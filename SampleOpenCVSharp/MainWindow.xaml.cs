using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp.WpfExtensions;
using OpenCvSharp;

namespace SampleOpenCVSharp
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        //项目说明：
        //1  开启摄像头之后在某些机器上可能会非常慢，要等一会，解决办法是new VideoCapture(0, VideoCaptureAPIs.DSHOW)加个DSHOW的参数，但这样又会造成录像失败，目前还没解决
        //2  项目为.Net 6，代码理论上适用于.Net 和 .NetFramework，但实测发现，.Net Framework下无法录像
        //3  通过nuget安装OpenCVSharp4.Windows和OpenCVSharp4.WpfExtensions，后者用于图像转换
        //4  图片和视频输出到执行文件所在目录下的"_SampleOpenCVSharp"子目录之中
        //参考文档：
        //opencvsharp官网   https://github.com/shimat/opencvsharp   （Apache-2.0协议）
        //OpenCVSharp官网wiki  https://github.com/shimat/opencvsharp/wiki  
        //opencv sharp 库使用手册    https://shimat.github.io/opencvsharp_docs/html/d69c29a1-7fb1-4f78-82e9-79be971c3d03.htm



        //设定图像分辨率，可以通过windows的搜索框->相机->设置获取当前摄像头支持分辨率，然后更改为想要的分辨率
        private int imgWidth = 1280;
        private int imgHeight = 720;

        private VideoCapture capture;        
        private VideoWriter videoWriter;

        private bool isCamOpen = false;
        private bool isRecording = false;
        private string recordState = "";

        private System.Timers.Timer recorderTimer;
        private DateTime startTime;



        public MainWindow()
        {
            InitializeComponent();

            //设置时间间隔ms
            int interval = 10;
            recorderTimer = new System.Timers.Timer(interval);
            //设置重复计时
            recorderTimer.AutoReset = true;
            //设置执行System.Timers.Timer.Elapsed事件
            recorderTimer.Elapsed += new System.Timers.ElapsedEventHandler(recorderTimer_Tick);

            //改变ui内容
            btnOpen.IsEnabled = true;
            btnPhoto.IsEnabled = false;
            btnRecord.IsEnabled = false;
        }

        private void recorderTimer_Tick(object sender, System.Timers.ElapsedEventArgs e)
        {
            //System.Timers.Timer 计时不准确，只能用本地时钟对比时差
            TimeSpan temp = TimeSpan.FromTicks(DateTime.Now.Ticks - startTime.Ticks);
            Action actionTimer = () =>
            {
                tbRecord.Text = recordState + string.Format("{0:00}:{1:00}:{2:00}", temp.Minutes, temp.Seconds, temp.Milliseconds / 10);
            };
            this.Dispatcher.BeginInvoke(actionTimer);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            btnOpen.IsEnabled = false;
            isCamOpen = true;

            Thread threadA = new Thread(runCam);
            threadA.Start();

            //改变UI
            btnPhoto.IsEnabled = true;
            btnRecord.IsEnabled = true;
        }



        void runCam()
        {
            //打开第一个摄像头
            capture = new VideoCapture(0);
            //如果摄像头打开太慢，可以尝试下面的方式，但是录像功能就不好用了，原因未知
            //capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
            //设置分辨率和帧数
            capture.Set(VideoCaptureProperties.FrameWidth, imgWidth);
            capture.Set(VideoCaptureProperties.FrameHeight, imgHeight);
            capture.Set(VideoCaptureProperties.Fps, 10);

            OpenCvSharp.Size dSize = new OpenCvSharp.Size(capture.FrameWidth, capture.FrameHeight);


            string outPath = System.Environment.CurrentDirectory + "\\_SampleOpenCVSharp";
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + imgWidth.ToString() + "x" + imgHeight.ToString() + ".avi";
            // Read movie frames and write them to VideoWriter 
            // XVID对应输出avi   HEVC 对应mp4 但输出有问题    h264 输出mkv 但输出有问题
            videoWriter = new VideoWriter(outPath + "\\" + fileName, FourCC.XVID, capture.Fps, dSize);
            using (Mat frame = new Mat())
            using (Mat gray = new Mat())
            using (Mat canny = new Mat())
            using (Mat dst = new Mat())
            {
                //if (capture.IsOpened()) { }

                while (isCamOpen)
                {
                    // Read image
                    capture.Read(frame);

                    if (frame.Empty())
                        break;
                    Action actionOpen = () =>
                    {
                        //使用OpenCVSharp4.WpfExtensions进行图像转换
                        var bi = WriteableBitmapConverter.ToWriteableBitmap(frame);
                        imgPreview.Source = bi;

                    };
                    this.Dispatcher.BeginInvoke(actionOpen);

                    //一些图像设置
                    // grayscale -> canny -> resize
                    //Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
                    //Cv2.Canny(gray, canny, 100, 180);
                    //Cv2.Resize(canny, dst, dsize, 0, 0, InterpolationFlags.Linear);

                    // Write mat to VideoWriter
                    if (isRecording)
                    {
                        videoWriter.Write(frame);
                    }

                }
                capture.Release();
                videoWriter.Release();
            }
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                startTime = DateTime.Now;
                recorderTimer.Start();

                videoWriter.Release();
                string outPath = System.Environment.CurrentDirectory + "\\_SampleOpenCVSharp";
                if (!Directory.Exists(outPath))
                {
                    Directory.CreateDirectory(outPath);
                }
                string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + imgWidth.ToString() + "x" + imgHeight.ToString() + ".avi";

                OpenCvSharp.Size dSize = new OpenCvSharp.Size(imgWidth, imgHeight);
                videoWriter = new VideoWriter(outPath + "\\" + fileName, FourCC.XVID, capture.Fps, dSize);


                //更改UI内容
                btnRecord.Content = "停止录像";
                btnPhoto.IsEnabled = false;

                isRecording = true;
                recordState = "正在录像 ";
            }
            else
            {
                recordState = "录像停止 ";
                btnRecord.Content = "开始录像";

                isRecording = false;


                videoWriter.Release();


                recorderTimer.Stop();
                //await mCaptue.StopRecordAsync();

                TimeSpan temp = TimeSpan.FromTicks(DateTime.Now.Ticks - startTime.Ticks);
                tbRecord.Text = recordState + string.Format("{0:00}:{1:00}:{2:00}", temp.Minutes, temp.Seconds, temp.Milliseconds / 10);

                btnPhoto.IsEnabled = true;
            }


        }

        private void btnPhoto_Click(object sender, RoutedEventArgs e)
        {
            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create((BitmapSource)imgPreview.Source));
            string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + imgWidth.ToString() + "x" + imgHeight.ToString() + ".jpg";
            string outPath = System.Environment.CurrentDirectory + "\\_SampleOpenCVSharp";
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            FileStream file = new FileStream(outPath + "\\" + fileName, FileMode.Create);
            encoder.Save(file);
            file.Close();

            //MessageBox.Show("图片已保存在'_SampleOpenCVSharp'目录之中");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            isCamOpen = false;
        }
    }
}
