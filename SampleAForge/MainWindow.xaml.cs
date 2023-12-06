using AForge.Video;
using Accord.Video.FFMPEG;
using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
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



namespace SampleAForge
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 项目说明：
        // 1. 项目为.Net Framework，使用AForge库，适用于.Net Framework和.Net，但库比较老了,录像功能在.Net下无法使用, 而且存在一些问题，使用起来也比较麻烦
        // 2. 首先在nuget下安装Aforge.controls,Aforge.video,Aforge.video.DirectShow
        // 3. 通过nuget安装Accord.Video.FFMPEG  用于编码为视频文件，但因为使用了ffmpeg解码器，所以Accord.Video.FFMPEG采用GPL协议，请注意协议的问题
        //    注意不要去找AForge.video.ffmpeg，已经很老了，很多坑，再说也找不到了
        // 4. 添加system.drawing的引用，如果是.Net6，需要在nuget之中安装 system.drawing.common
        // 5. 添加对WindowsFormsIntegration 程序集的引用，为了在XAML之中使用WindowsFormsHost
        // 6. 输出文件保存在可执行文件所在目录的_SampleAforge目录下
        // 参考文档
        // AForge官网 https://github.com/andrewkirillov/AForge.NET  如今是Accord的一部分 使用ffmpeg的部分为GPL协议，其他代码为LGPL协议
        // Accord官网 http://accord-framework.net/index.html  部分为GPL协议，其他代码为LGPL协议


        private int videoWidth;
        private int videoHeight;
        private int fps;

        private VideoCaptureDevice videoSource;
        private FilterInfoCollection videoDevices;

        private VideoFileWriter videoWriter;   //写入到视频


        private System.Timers.Timer recorderTimer;
        private DateTime startTime;

        private bool isRecording = false;
        private string recordState = "";

        public MainWindow()
        {
            InitializeComponent();


            videoWriter = new VideoFileWriter();


            //设置时间间隔ms
            int interval = 10;
            recorderTimer = new System.Timers.Timer(interval);
            //设置重复计时
            recorderTimer.AutoReset = true;
            //设置执行System.Timers.Timer.Elapsed事件
            recorderTimer.Elapsed += new System.Timers.ElapsedEventHandler(recorderTimer_Tick);



            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            if (videoDevices.Count == 0)
            {
                System.Windows.MessageBox.Show("未检测到摄像头，请确认！");
                return;
            }

            //获取摄像头分辨率
            videoSource = new VideoCaptureDevice(videoDevices[0].MonikerString);//连接摄像头

            cbVideo.Items.Clear();
            foreach (var videoCap in videoSource.VideoCapabilities)
            {
                System.Windows.Controls.ComboBoxItem comboBoxItem = new System.Windows.Controls.ComboBoxItem();
                comboBoxItem.Content = videoCap.FrameSize.Width.ToString() + "x" + videoCap.FrameSize.Height.ToString() + " fps:" + videoCap.AverageFrameRate.ToString();
                comboBoxItem.Tag = videoCap;
                cbVideo.Items.Add(comboBoxItem);
            }

            cbVideo.SelectedIndex = 0;


            //改变ui内容
            btnOpen.IsEnabled = true;
            btnPhoto.IsEnabled = false;
            btnRecord.IsEnabled = false;
            cbVideo.IsEnabled = true;

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


        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            videoSourcePlayer.Stop();
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            btnOpen.IsEnabled = false;
            cbVideo.IsEnabled = false;

            videoSource.VideoResolution = videoSource.VideoCapabilities[cbVideo.SelectedIndex];
            videoSourcePlayer.VideoSource = videoSource;


            videoWidth = videoSource.VideoResolution.FrameSize.Width;
            videoHeight = videoSource.VideoResolution.FrameSize.Height;
            fps = videoSource.VideoResolution.AverageFrameRate;

            videoSource.NewFrame += new NewFrameEventHandler(showVideo);

            videoSource.Start();

            btnRecord.IsEnabled = true;
            btnPhoto.IsEnabled = true;
        }

        //新帧的触发函数
        private void showVideo(object sender, NewFrameEventArgs eventArgs)
        {
            //if (isshowed)
            //{
            //    this.label5.Visible = false;
            //    isshowed = false;
            //}
            Bitmap bitmap = eventArgs.Frame;  //获取到一帧图像

            if (isRecording)
            {
                videoWriter.WriteVideoFrame(bitmap);
            }
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                startTime = DateTime.Now;
                recorderTimer.Start();


                string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + videoWidth.ToString() + "x" + videoHeight.ToString();
                string outPath = System.Environment.CurrentDirectory + "\\_SampleAforge";
                if (!Directory.Exists(outPath))
                {
                    Directory.CreateDirectory(outPath);
                }

                //创建一个视频文件
                String video_format = "MPEG"; //获取选中的视频编码
                if (this.videoSource.IsRunning && this.videoSourcePlayer.IsRunning)
                {
                    if (-1 != video_format.IndexOf("MPEG")) //分辨率输出不正确
                    {
                        videoWriter.Open(outPath + "\\" + fileName + ".avi", videoWidth, videoHeight, fps, VideoCodec.MPEG4);
                    }
                    else if (-1 != video_format.IndexOf("WMV"))
                    {
                        videoWriter.Open(outPath + "\\" + fileName + ".wmv", videoWidth, videoHeight, fps, VideoCodec.WMV1);
                    }
                    else if (-1 != video_format.IndexOf("h264")) //无法正常保存
                    {
                        videoWriter.Open(outPath + "\\" + fileName + ".mp4", videoWidth, videoHeight, fps, VideoCodec.H264);
                    }
                    else
                    {
                        videoWriter.Open(outPath + "\\" + fileName + ".mkv", videoWidth, videoHeight, fps, VideoCodec.Default);
                    }
                }
                else
                {

                }


                //更改UI内容
                btnRecord.Content = "停止录像";
                isRecording = true;

                btnPhoto.IsEnabled = false;


                recordState = "正在录像 ";
            }
            else
            {
                // Reflect the changes in UI and stop recording
                recordState = "录像停止 ";
                btnRecord.Content = "开始录像";
                isRecording = false;

                recorderTimer.Stop();

                videoWriter.Close();

                TimeSpan temp = TimeSpan.FromTicks(DateTime.Now.Ticks - startTime.Ticks);
                tbRecord.Text = recordState + string.Format("{0:00}:{1:00}:{2:00}", temp.Minutes, temp.Seconds, temp.Milliseconds / 10);

                btnPhoto.IsEnabled = true;

                //MessageBox.Show("视频已保存在'_SampleAforge'目录之中");
            }

        }

        private void btnPhoto_Click(object sender, RoutedEventArgs e)
        {
            if (videoSource == null)
                return;
            Bitmap bitmap = videoSourcePlayer.GetCurrentVideoFrame();
            string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + videoWidth.ToString() + "x" + videoHeight.ToString() + ".jpg";
            string outPath = System.Environment.CurrentDirectory + "\\_SampleAforge";
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
            bitmap.Save(outPath + "\\" + fileName, System.Drawing.Imaging.ImageFormat.Jpeg);

            //MessageBox.Show("图片已保存在'_SampleAforge'目录之中");
        }

        private void cbVideo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

    }

}
