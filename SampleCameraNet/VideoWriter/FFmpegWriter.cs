using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace SampleCameraNet
{
    /// <summary>
    /// Encode Video using FFmpeg.exe
    /// </summary>
    public class FFmpegWriter
    {
        readonly Process _ffmpegProcess;
        readonly NamedPipeServerStream _ffmpegIn;
        byte[] _videoBuffer;

        // This semaphore helps prevent FFmpeg audio/video pipes getting deadlocked.
        readonly SemaphoreSlim _spVideo = new SemaphoreSlim(5);

        // Timeout used with Semaphores, if elapsed would mean FFmpeg might be deadlocked.
        readonly TimeSpan _spTimeout = TimeSpan.FromMilliseconds(50);

        static string GetPipeName() => $"SmartCam-{Guid.NewGuid()}";

        /// <summary>
        /// Creates a new instance of <see cref="FFmpegWriter"/>.
        /// </summary>
        public FFmpegWriter(int width, int height)
        {
            if (!FFmpegService.FFmpegExists)
            {
                throw new Exception();
            }

            var nv12 = false;// Args.ImageProvider.DummyFrame is INV12Frame;

            //var settings = ServiceProvider.Get<FFmpegSettings>();

            var w = width;// Args.ImageProvider.Width;
            var h = height;// Args.ImageProvider.Height;

            _videoBuffer = new byte[(int)(w * h * (nv12 ? 1.5 : 4))];

            Console.WriteLine($"Video Buffer Allocated: {_videoBuffer.Length}");

            var videoPipeName = GetPipeName();


            string FileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + width.ToString() + "x" + height.ToString();

            //方便起见，删掉了大量Captura的代码
            //var argsBuilder = new FFmpegArgsBuilder();

            //argsBuilder.AddInputPipe(videoPipeName)
            //    .AddArg("thread_queue_size", 512)
            //    .AddArg("framerate", Args.FrameRate)
            //    .SetFormat("rawvideo")
            //    .AddArg("pix_fmt", nv12 ? "nv12" : "rgb32")
            //    .SetVideoSize(w, h);

            //var output = argsBuilder.AddOutputFile(Args.FileName)
            //    .SetFrameRate(Args.FrameRate);

            //Args.VideoCodec.Apply(settings, Args, output);

            /*
            if (settings.Resize)
            {
                var width = settings.ResizeWidth;
                var height = settings.ResizeHeight;

                if (width % 2 == 1)
                    ++width;

                if (height % 2 == 1)
                    ++height;

                output.AddArg("vf", $"scale={width}:{height}");
            }
            */



            _ffmpegIn = new NamedPipeServerStream(videoPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, _videoBuffer.Length);
            //
            //方便起见，简化处理
            _ffmpegProcess = FFmpegService.StartFFmpeg("-thread_queue_size 512 -framerate 25 -f rawvideo -pix_fmt rgb32 -video_size 1280x720 -i \\\\.\\pipe\\" + videoPipeName + " -r 10 -vcodec libx264 -crf 15 -pix_fmt yuv420p -preset fast \".\\_SampleCameraNet\\" + FileName + ".mp4\"", ".\\_SampleCameraNet\\" + FileName + ".mp4");
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _lastFrameTask?.Wait();

            _ffmpegIn.Dispose();

            _ffmpegProcess.WaitForExit();

            _videoBuffer = null;
        }



        bool _firstFrame = true;

        bool _initialStability;
        int _frameStreak;
        const int FrameStreakThreshold = 50;
        int _skippedFrames;


        Task _lastFrameTask;

        /// <summary>
        /// Writes an Image frame.
        /// </summary>
        public void WriteFrame(IBitmapFrame Frame)
        {
            if (_ffmpegProcess.HasExited)
            {
                Frame.Dispose();
                throw new Exception();
            }

            if (_firstFrame)
            {
                if (!_ffmpegIn.WaitForConnection(5000))
                {
                    throw new Exception("Cannot connect Video pipe to FFmpeg");
                }

                _firstFrame = false;
            }

            if (_lastFrameTask == null)
            {
                _lastFrameTask = Task.CompletedTask;
            }

            if (!(Frame is RepeatFrame))
            {
                using (Frame)
                {
                    if (Frame.Unwrap() is INV12Frame nv12Frame)
                    {
                        nv12Frame.CopyNV12To(_videoBuffer);
                    }
                    else Frame.CopyTo(_videoBuffer);
                }
            }

            // Drop frames if semaphore cannot be acquired soon enough.
            // Frames are dropped mostly in the beginning of recording till atleast one audio frame is received.
            if (!_spVideo.Wait(_spTimeout))
            {
                ++_skippedFrames;
                _frameStreak = 0;
                return;
            }

            // Most of the drops happen in beginning of video, once that stops, sync can be done.
            if (!_initialStability)
            {
                ++_frameStreak;
                if (_frameStreak > FrameStreakThreshold)
                {
                    _initialStability = true;
                }
            }

            try
            {
                // Check if last write failed.
                if (_lastFrameTask != null && _lastFrameTask.IsFaulted)
                {
                    _lastFrameTask.Wait();
                }

                _lastFrameTask = _lastFrameTask.ContinueWith(async M =>
                {
                    try
                    {
                        await _ffmpegIn.WriteAsync(_videoBuffer, 0, _videoBuffer.Length);
                    }
                    finally
                    {
                        _spVideo.Release();
                    }
                });
            }
            catch (Exception e) when (_ffmpegProcess.HasExited)
            {
                throw new Exception();
            }
        }
    }
}
