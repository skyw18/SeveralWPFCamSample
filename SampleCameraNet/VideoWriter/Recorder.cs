using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Camera_NET;
using DirectShowLib;

// ReSharper disable MethodSupportsCancellation

namespace SampleCameraNet
{
    /// <summary>
    /// Default implementation of <see cref="IRecorder"/> interface.
    /// Can output to <see cref="IVideoFileWriter"/> or <see cref="IAudioFileWriter"/>.
    /// </summary>
    public class Recorder
    {
        #region Fields
        FFmpegWriter _videoWriter;
        IImageProvider _imageProvider;
        //CaptureWebcam _captureWebcam;
        CameraControl _cameraCon;

        readonly int _frameRate;

        readonly Stopwatch _sw;

        readonly ManualResetEvent _continueCapturing;
        readonly CancellationTokenSource _cancellationTokenSource;
        readonly CancellationToken _cancellationToken;

        readonly Task _recordTask;

        readonly object _syncLock = new object();

        Task<bool> _frameWriteTask;
        Task _audioWriteTask;
        int _frameCount;
        long _audioBytesWritten;
        readonly int _audioBytesPerFrame, _audioChunkBytes;
        const int AudioChunkLengthMs = 200;
        byte[] _audioBuffer, _silenceBuffer;

        //readonly IFpsManager _fpsManager;
        #endregion

        /// <summary>
        /// Creates a new instance of <see cref="IRecorder"/> writing to <see cref="IVideoFileWriter"/>.
        /// </summary>
        /// <param name="VideoWriter">The <see cref="IVideoFileWriter"/> to write to.</param>
        /// <param name="ImageProvider">The image source.</param>
        /// <param name="FrameRate">Video Frame Rate.</param>
        /// <param name="AudioProvider">The audio source. null = no audio.</param>
        //public Recorder(IVideoFileWriter VideoWriter, IImageProvider ImageProvider, int FrameRate)//,
        //IAudioProvider AudioProvider = null,
        //IFpsManager FpsManager = null)

        //做了修改，以配合CameraControl
        public Recorder(FFmpegWriter VideoWriter, CameraControl cc, int FrameRate)// CaptureWebcam cw, int FrameRate)//,
        {

            _videoWriter = VideoWriter ?? throw new ArgumentNullException(nameof(VideoWriter));
            //_imageProvider = ImageProvider ?? throw new ArgumentNullException(nameof(ImageProvider));
            //_fpsManager = FpsManager;
            //_captureWebcam = cw;
            _cameraCon = cc;
            _imageProvider = null;
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;

            if (FrameRate <= 0)
                throw new ArgumentException("Frame Rate must be possitive", nameof(FrameRate));

            _frameRate = FrameRate;

            _continueCapturing = new ManualResetEvent(false);



            _sw = new Stopwatch();

            _recordTask = Task.Factory.StartNew(async () => await DoRecord(), TaskCreationOptions.LongRunning);
        }

        async Task DoRecord()
        {
            try
            {
                var frameInterval = TimeSpan.FromSeconds(1.0 / _frameRate);
                _frameCount = 0;

                // Returns false when stopped

                while (_continueCapturing.WaitOne() && !_cancellationToken.IsCancellationRequested)
                {
                    var timestamp = _sw.Elapsed;

                    if (_frameWriteTask != null)
                    {
                        // If false, stop recording
                        if (!await _frameWriteTask)
                            return;

                        if (!WriteDuplicateFrame())
                            return;
                    }


                    _frameWriteTask = Task.Run(() => FrameWriter(timestamp));

                    var timeTillNextFrame = timestamp + frameInterval - _sw.Elapsed;

                    if (timeTillNextFrame > TimeSpan.Zero)
                        Thread.Sleep(timeTillNextFrame);
                }
            }
            catch (Exception e)
            {
                lock (_syncLock)
                {
                    if (!_disposed)
                    {
                        ErrorOccurred?.Invoke(e);

                        Dispose(false);
                    }
                }
            }
        }
        readonly SyncContextManager _syncContext = new SyncContextManager();
        public IBitmapImage CaptureBitmap(IBitmapLoader BitmapLoader)
        {
            //return _syncContext.Run(() => _captureWebcam.GetFrame());
            return _syncContext.Run(() => new DrawingImage(_cameraCon.SnapshotSourceImage()));
            //return _captureWebcam.GetFrame();
        }

        public IEditableFrame Capture()
        {
            try
            {
                //_syncContext.Run(() => _captureWebcam.GetFrame(BitmapLoader));
                //var img = _webcamCapture.Value?.Capture(GraphicsBitmapLoader.Instance);
                var img = CaptureBitmap(GraphicsBitmapLoader.Instance);
                if (img is DrawingImage drawingImage && drawingImage.Image is Bitmap bmp)
                    return new GraphicsEditor(bmp);

                return RepeatFrame.Instance;
            }
            catch
            {
                return RepeatFrame.Instance;
            }
        }

        bool FrameWriter(TimeSpan Timestamp)
        {
            //return new GraphicsEditor(bmp);
            //var editableFrame = _imageProvider.Capture();            

            var editableFrame = Capture();

            var frame = editableFrame.GenerateFrame(Timestamp);

            var success = AddFrame(frame);

            if (!success)
            {
                return false;
            }

            //_fpsManager?.OnFrame();

            return true;
        }

        bool WriteDuplicateFrame()
        {
            var requiredFrames = _sw.Elapsed.TotalSeconds * _frameRate;
            var diff = requiredFrames - _frameCount;

            // Write atmost 1 duplicate frame
            if (diff >= 1)
            {
                if (!AddFrame(RepeatFrame.Instance))
                    return false;
            }

            return true;
        }

        bool AddFrame(IBitmapFrame Frame)
        {
            try
            {
                _videoWriter.WriteFrame(Frame);

                ++_frameCount;

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }


        #region Dispose
        async void Dispose(bool TerminateRecord)
        {
            if (_disposed)
                return;

            _disposed = true;

            _cancellationTokenSource.Cancel();

            // Resume record loop if paused so it can exit
            _continueCapturing.Set();

            // Ensure all threads exit before disposing resources.
            if (TerminateRecord)
                _recordTask.Wait();

            try
            {
                if (_frameWriteTask != null)
                    await _frameWriteTask;
            }
            catch { }

            //_imageProvider?.Dispose();
            //_imageProvider = null;

            _videoWriter.Dispose();
            _videoWriter = null;


            _continueCapturing.Dispose();
        }

        /// <summary>
        /// Frees all resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            lock (_syncLock)
            {
                Dispose(true);
            }
        }

        bool _disposed;

        /// <summary>
        /// Fired when an error occurs
        /// </summary>
        public event Action<Exception> ErrorOccurred;

        void ThrowIfDisposed()
        {
            lock (_syncLock)
            {
                if (_disposed)
                    throw new ObjectDisposedException("this");
            }
        }
        #endregion

        /// <summary>
        /// Start Recording.
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            _sw?.Start();

            _continueCapturing?.Set();
        }

        /// <summary>
        /// Stop Recording.
        /// </summary>
        public void Stop()
        {
            ThrowIfDisposed();

            _continueCapturing?.Reset();

            _sw?.Stop();
        }
    }
}
