using Camera_NET;
using System;
using System.Collections.Generic;
using System.Drawing;
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

namespace SampleCameraNet
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 1. 项目为.Net Framework，代码适用于.Net和.Net Framework。可以从摄像头录像截屏，切换分辨率。
        //    切换分辨率和获取摄像头内容的代码来自Camera_Net，使用DirectShowLib库来实现，录像和截图的代码来自Captura，使用ffmpeg编码输出mp4
        // 2. ffmpeg需从官网下载GPL版的，将可执行文件和dll解压到指定目录，比如D:\ffmpeg，然后将FFmpegService.cs中的两处folderPath改为该目录
        // 3. 从nuget之中添加DirectShowLib，添加对Camera_Net.dll的引用
        // 4. 添加System.Drawing程序集 用于输出图片
        // 5. Camera_Net为一套显示摄像头图像的winform控件UserControl，所以需要添加System.Windows.Forms程序集和WindowsFormsIntegration 程序集，以在wpf之中通过WindowsFormsHost 使用winform控件 
        // 6. 输出文件保存在可执行文件所在目录的_SampleCameraNet目录下
        // 7. 如果显示XAML 设计器已意外退出。(退出代码: e0434352)，并且XAML设计界面没有内容，说明上述库没有引入完全，Camera_Net需要DirectShowLib和System.Windows.Forms，引入完全之后可以正常显示
        //
        //参考文档
        // Camera_Net官网 https://github.com/free5lot/Camera_Net     LGPL 3.0 协议
        // DirectShowLib官网： https://directshownet.sourceforge.net/   LGPL 2.1协议
        // Captura官网 https://github.com/MathewSachin/Captura    MIT协议
        // FFmpeg官网 https://ffmpeg.org/    GPL协议


        bool IsRecording = false;
        ResolutionList resolutions;
        CameraChoice _CameraChoice = new CameraChoice();

        Recorder _recorder;
        FFmpegWriter _videoWriter;


        string recordState = "";

        System.Timers.Timer recorderTimer;
        DateTime startTime;

        int VideoWidth;
        int VideoHeight;

        public MainWindow()
        {
            InitializeComponent();

            //改变ui内容
            //cbVideo.IsEnabled = false;
            btnOpen.IsEnabled = true;
            btnPhoto.IsEnabled = false;
            btnRecord.IsEnabled = false;

            _CameraChoice.UpdateDeviceList();

            cbVideo.Items.Clear();
            resolutions = Camera_NET.Camera.GetResolutionList(_CameraChoice.Devices[0].Mon);
            if (resolutions == null)
                return;

            foreach (var tmp_resolution in resolutions)
            {
                System.Windows.Controls.ComboBoxItem comboBoxItem = new System.Windows.Controls.ComboBoxItem();
                comboBoxItem.Content = tmp_resolution.Width.ToString() + "x" + tmp_resolution.Height.ToString();
                comboBoxItem.Tag = tmp_resolution;
                cbVideo.Items.Add(comboBoxItem);
            }
            cbVideo.SelectedIndex = 0;

            VideoWidth = resolutions[0].Width;
            VideoHeight = resolutions[0].Height;

            //设置时间间隔ms
            int interval = 10;
            recorderTimer = new System.Timers.Timer(interval);
            //设置重复计时
            recorderTimer.AutoReset = true;
            //设置执行System.Timers.Timer.Elapsed事件
            recorderTimer.Elapsed += new System.Timers.ElapsedEventHandler(recorderTimer_Tick);
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

        private void cbVideo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!cameraCon.CameraCreated)
                return;

            int comboBoxResolutionIndex = cbVideo.SelectedIndex;
            if (comboBoxResolutionIndex < 0)
            {
                return;
            }
            ResolutionList resolutions = Camera.GetResolutionList(cameraCon.Moniker);

            if (resolutions == null)
                return;

            if (comboBoxResolutionIndex >= resolutions.Count)
                return; // throw

            if (0 == resolutions[comboBoxResolutionIndex].CompareTo(cameraCon.Resolution))
            {
                // this resolution is already selected
                return;
            }
            VideoWidth = resolutions[comboBoxResolutionIndex].Width;
            VideoHeight = resolutions[comboBoxResolutionIndex].Height;
            cameraCon.SetCamera(cameraCon.Moniker, resolutions[comboBoxResolutionIndex]);
        }

        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            btnOpen.IsEnabled = false;
            btnRecord.IsEnabled = true;
            btnPhoto.IsEnabled = true;
            cbVideo.IsEnabled = true;

            cameraCon.SetCamera(_CameraChoice.Devices[0].Mon, resolutions[cbVideo.SelectedIndex]);   
        }

        private void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!IsRecording)
            {
                string outPath = System.Environment.CurrentDirectory + "\\_SampleCameraNet";
                if (!Directory.Exists(outPath))
                {
                    Directory.CreateDirectory(outPath);
                }
                string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + VideoWidth.ToString() + "x" + VideoHeight.ToString() + ".mp4";

                _videoWriter = new FFmpegWriter(VideoWidth, VideoHeight);
                //_videoWriter.WriteFrame(_captureWebcam.GetFrame(null));

                _recorder = new Recorder(_videoWriter, cameraCon, 25);
                _recorder.Start();

                startTime = DateTime.Now;
                recorderTimer.Start();



                //更改UI内容
                btnRecord.Content = "停止录像";
                btnPhoto.IsEnabled = false;
                cbVideo.IsEnabled = false;

                IsRecording = true;
                recordState = "正在录像 ";
            }
            else
            {
                recordState = "录像停止 ";
                btnRecord.Content = "开始录像";

                IsRecording = false;

                _recorder.Stop();
                _recorder.Dispose();

                recorderTimer.Stop();
                //await mCaptue.StopRecordAsync();

                TimeSpan temp = TimeSpan.FromTicks(DateTime.Now.Ticks - startTime.Ticks);
                tbRecord.Text = recordState + string.Format("{0:00}:{1:00}:{2:00}", temp.Minutes, temp.Seconds, temp.Milliseconds / 10);

                btnPhoto.IsEnabled = true;
                cbVideo.IsEnabled = true;
            }
        }

        private void btnPhoto_Click(object sender, RoutedEventArgs e)
        {
            string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + VideoWidth.ToString() + "x" + VideoHeight.ToString() + ".jpg";
            string outPath = System.Environment.CurrentDirectory + "\\_SampleCameraNet";
            if (!Directory.Exists(outPath))
            {
                Directory.CreateDirectory(outPath);
            }
                  
            Bitmap bmp = cameraCon.Camera.SnapshotOutputImage();
            bmp.Save(outPath + "\\" + fileName, System.Drawing.Imaging.ImageFormat.Jpeg);

            //MessageBox.Show("图片已保存在'_SampleCameraNet'目录之中");
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!btnOpen.IsEnabled)
            {
                cameraCon.CloseCamera();
            }
        }
    }
}
