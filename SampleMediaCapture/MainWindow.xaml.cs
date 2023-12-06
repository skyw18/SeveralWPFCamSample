using System;
using System.Collections.Generic;
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
using MediaCaptureWPF;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using System.Threading;
using System.Timers;

namespace SampleMediaCapture
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// 
    ///  
    /// </summary>
    public partial class MainWindow : Window
    {
        //项目说明：  
        //1. 项目为.Net Framework，代码只适用于.Net Framework，因为.Net5之后放弃了对WinRT的支持，无法使用MediaCapture类
        //2. Visual Studio开发环境需安装"工作负荷-使用C++的桌面开发"，因为下面的MediaCaptureWPF.Native.dll使用了本地代码
        //3. 添加MediaCaptureWPF 和 MediaCaptureWPF.Native 引用，dll放在dll子目录，或者去https://github.com/mmaitre314/MediaCaptureWPF
        //4. 在 工具 -> Nuget包管理器 -> 程序包管理器设置 -> 常规 之中将默认包管理器模式改为 PackageReference
        //5. 在Nuget之中添加 Microsoft.Toolkit.Wpf.UI.Controls 
        //6. 添加StreamResolution类，以用于切换分辨率
        //7. 项目属性之中 目标平台确保为x64
        //8. 图片和视频输出到"我的图片\_SampleMediaCapture",因为MediaCapture类移植自UWP,故此只能对我的图片目录进行操作
        //   保存位置可以通过改写StorageFolder来实现，但只适用于WinRT，在.net下会报错System.InvalidOperationException
        //   修改保存位置的说明文档可以参考  https://learn.microsoft.com/zh-cn/uwp/api/windows.storage.storagefolder.getfolderfrompathasync?view=winrt-19041
        //参考文档：
        //一些代码来自微软官方实例 https://github.com/microsoft/Windows-universal-samples/tree/main/Samples/CameraResolution
        //MediaCaptureWPF  https://github.com/mmaitre314/MediaCaptureWPF   （Apache-2.0协议）

        public MediaCapture mCaptue;
        public CapturePreview cPreview;
        private StorageFolder cFolder = null;
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
            cbVideo.IsEnabled = false;
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

        private async void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            btnOpen.IsEnabled = false;


            mCaptue = new MediaCapture();
            await mCaptue.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video // No audio
            });

            cPreview = new CapturePreview(mCaptue);
            imgPreview.Source = cPreview;
            await cPreview.StartAsync();

            //搜索设备所有分辨率
            IEnumerable<StreamResolution> allVideoProperties = mCaptue.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord).Select(x => new StreamResolution(x));

            //查询当前分辨率
            StreamResolution previewProperties = new StreamResolution(mCaptue.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview));

            // Get all formats that have the same-ish aspect ratio as the preview
            // Allow for some tolerance in the aspect ratio comparison
            const double ASPECT_RATIO_TOLERANCE = 0.015;
            var matchingFormats = allVideoProperties;//.Where(x => Math.Abs(x.AspectRatio - previewProperties.AspectRatio) < ASPECT_RATIO_TOLERANCE);

            // Order them by resolution then frame rate
            allVideoProperties = matchingFormats.OrderByDescending(x => x.Height * x.Width).ThenByDescending(x => x.FrameRate);


            cbVideo.Items.Clear();
            int _index = 0;
            foreach (var property in allVideoProperties)
            {
                System.Windows.Controls.ComboBoxItem comboBoxItem = new System.Windows.Controls.ComboBoxItem();
                comboBoxItem.Content = property.GetFriendlyName();
                comboBoxItem.Tag = property;
                cbVideo.Items.Add(comboBoxItem);

                if(previewProperties.GetFriendlyName() == property.GetFriendlyName())
                {
                    cbVideo.SelectedIndex = _index;
                }
                _index++;
            }

            //改变ui内容
            cbVideo.IsEnabled = true;
            btnPhoto.IsEnabled = true;
            btnRecord.IsEnabled = true;
        }

        private async void cbVideo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cbVideo.SelectedIndex > -1)
            {
                var selectedItem = (sender as System.Windows.Controls.ComboBox).SelectedItem as System.Windows.Controls.ComboBoxItem;
                var encodingProperties = (selectedItem.Tag as StreamResolution).EncodingProperties;

                await mCaptue.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoRecord, encodingProperties);

                //获取当前分辨率
                var props = (VideoEncodingProperties)mCaptue.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                string tmpResolution = props.Width.ToString() + "x" + props.Height.ToString();

                tbRecord.Text = tmpResolution;
            }
        }

        private async void btnRecord_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecording)
            {
                try
                {
                    //获取当前分辨率
                    var props = (VideoEncodingProperties)mCaptue.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                    string tmpResolution = props.Width.ToString() + "x" + props.Height.ToString();

                    string FileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + tmpResolution + ".mp4";

                    var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);

                    // Fall back to the local app storage if the Pictures Library is not available
                    cFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;

                    // Create storage file in Video Library
                    var videoFile = await cFolder.CreateFileAsync("_SampleMeidaCaptureOut\\" + FileName, CreationCollisionOption.GenerateUniqueName);

                    // 根据需要选择不同视频输出格式，别忘记改上面的扩展名
                    //var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto); //h.264
                    //var encodingProfile = MediaEncodingProfile.CreateAvi(VideoEncodingQuality.Auto);
                    //var encodingProfile = MediaEncodingProfile.CreateWmv(VideoEncodingQuality.Auto);
                    var encodingProfile = MediaEncodingProfile.CreateHevc(VideoEncodingQuality.Auto); //h.265


                    await mCaptue.StartRecordToStorageFileAsync(encodingProfile, videoFile);

                    startTime = DateTime.Now;
                    recorderTimer.Start();

                    //更改UI内容
                    btnRecord.Content = "停止录像";
                    isRecording = true;

                    cbVideo.IsEnabled = false;
                    btnPhoto.IsEnabled = false;


                    recordState = "正在录像 ";
                }
                catch (Exception ex)
                {
                    // File I/O errors are reported as exceptions.
                }
            }
            else
            {
                // Reflect the changes in UI and stop recording
                recordState = "录像停止 ";
                btnRecord.Content = "开始录像";
                isRecording = false;

                recorderTimer.Stop();
                await mCaptue.StopRecordAsync();

                TimeSpan temp = TimeSpan.FromTicks(DateTime.Now.Ticks - startTime.Ticks);
                tbRecord.Text = recordState + string.Format("{0:00}:{1:00}:{2:00}", temp.Minutes, temp.Seconds, temp.Milliseconds / 10);

                cbVideo.IsEnabled = true;
                btnPhoto.IsEnabled = true;

                //MessageBox.Show("视频已保存在'我的图片\_SampleMediaCapture'之中");
            }
        }

        private async void btnPhoto_Click(object sender, RoutedEventArgs e)
        {
            var stream = new InMemoryRandomAccessStream();

            try
            {
                //获取当前分辨率
                var props = (VideoEncodingProperties)mCaptue.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                string tmpResolution = props.Width.ToString() + "x" + props.Height.ToString();

                string FileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + tmpResolution + ".jpg";
                var picturesLibrary = await StorageLibrary.GetLibraryAsync(KnownLibraryId.Pictures);
                // Fall back to the local app storage if the Pictures Library is not available
                cFolder = picturesLibrary.SaveFolder ?? ApplicationData.Current.LocalFolder;
                // Take and save the photo
                var file = await cFolder.CreateFileAsync( FileName, CreationCollisionOption.GenerateUniqueName);
                await mCaptue.CapturePhotoToStorageFileAsync(ImageEncodingProperties.CreateJpeg(), file);
                
                //MessageBox.Show("照片已保存在'我的图片\_SampleMediaCapture'之中");
            }
            catch (Exception ex)
            {
                //出错处理
            }
        }
    }
}
