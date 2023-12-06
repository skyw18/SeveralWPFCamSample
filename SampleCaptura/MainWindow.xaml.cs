using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SampleCaptura
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //项目说明：
        //1. 项目为.Net Framework，代码适用于.Net Framework和.Net，可以获取从摄像头录像截图，但不能选择分辨率，
        //   获取摄像头图像和录像截图代码都抄自Captura，获取摄像头部分使用DirectShowLib，编码输出视频部分使用ffmpeg
        //2. ffmpeg需从官网下载GPL版的，将可执行文件和dll解压到指定目录，比如D:\ffmpeg，然后将FFmpegService.cs中的两处folderPath改为该目录
        //3. 从nuget之中添加DirectShowLib，添加对程序集System.Drawing的引用
        //4. 输出文件保存在可执行文件所在目录的_SampleCaptura目录下
        //参考文档：
        //DirectShowLib官网： https://directshownet.sourceforge.net/   LGPL 2.1协议
        //Captura官网   https://github.com/MathewSachin/Captura    MIT协议
        //FFmpeg官网  https://ffmpeg.org/    GPL协议

        private bool IsRecording = false;

        private Filter _filter;

        CaptureWebcam _captureWebcam;
        Recorder _recorder;
        FFmpegWriter _videoWriter;


        private static int VideoWidth;
        private static int VideoHeight;

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


            //自己根据摄像头写分辨率
            VideoWidth = 1280;
            VideoHeight = 720;
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
            btnRecord.IsEnabled = true;
            btnPhoto.IsEnabled = true;

            //获取摄像头列表 返回一个迭代器
            IEnumerable<Filter> webcamSources = Filter.VideoInputDevices;
            List<Filter> webcams = new List<Filter>();
            foreach (var webcam in webcamSources)
            {
                webcams.Add(webcam);
            }

            Action actionOpen = () =>
            {
                _captureWebcam = new CaptureWebcam(webcams[0], null, IntPtr.Zero, this);
                _captureWebcam.StartPreview();
                _captureWebcam.OnPreviewWindowResize(10, 90, VideoWidth, VideoHeight);

            };
            this.Dispatcher.BeginInvoke(actionOpen);
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRecording)
            {


                string outPath = System.Environment.CurrentDirectory + "\\_SampleCaptura";
                if (!Directory.Exists(outPath))
                {
                    Directory.CreateDirectory(outPath);
                }               

                _videoWriter = new FFmpegWriter(VideoWidth, VideoHeight);

                _recorder = new Recorder(_videoWriter, _captureWebcam, 25);
                _recorder.Start();


                startTime = DateTime.Now;
                recorderTimer.Start();

                //更改UI内容
                btnRecord.Content = "停止录像";
                btnPhoto.IsEnabled = false;

                IsRecording = true;
                recordState = "正在录像 ";
            }
            else
            {
                recordState = "录像停止 ";
                btnRecord.Content = "开始录像";


                _recorder.Stop();
                _recorder.Dispose();

                IsRecording = false;

                recorderTimer.Stop();
                //await mCaptue.StopRecordAsync();

                TimeSpan temp = TimeSpan.FromTicks(DateTime.Now.Ticks - startTime.Ticks);
                tbRecord.Text = recordState + string.Format("{0:00}:{1:00}:{2:00}", temp.Minutes, temp.Seconds, temp.Milliseconds / 10);
                
                btnPhoto.IsEnabled = true;
            }
        }

        private void btnPhoto_Click(object sender, RoutedEventArgs e)
        {
            string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + VideoWidth.ToString() + "x" + VideoHeight.ToString() + ".jpg";
            string outPath = System.Environment.CurrentDirectory + "\\_SampleCaptura";
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            _captureWebcam.GetFrame().Save(outPath + "\\" + fileName, ImageFormats.Jpg);

            //MessageBox.Show("图片已保存在输出目录的'_SampleCaptura'子目录之中");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(!btnOpen.IsEnabled)
            {
                Action actionClose = () =>
                {                    
                    _captureWebcam.StopPreview();
                    _captureWebcam.Dispose();

                };
                this.Dispatcher.BeginInvoke(actionClose);
            }

        }
    }
}
