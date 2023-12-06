using Microsoft.Web.WebView2.Core;
using SimpleJSON;
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

namespace SampleWebview
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //项目说明：
        //1  使用webview2控件实现，摄像机相关代码以前端方式通过JS实现，数据通过json传给后端，C#主要在后台进行保存视频和图片工作
        //2  项目为.Net Framework，应该可以适用于.Net6。
        //3  前端无法获取相机支持的分辨率和帧数列表，但是可以指定一个理想值，来设定最高分辨率，可以通过别的项目来获取分辨率列表，再指定
        //4  图片和视频输出到执行文件所在目录下的"_SampleWebview"子目录之中
        //5  使用simplejson处理json

        public MainWindow()
        {
            InitializeComponent();

            InitializeAsync();
        }

        async void InitializeAsync()
        {
            //加载之前需等待webview2 initialized完成。也可以设定source
            await webView.EnsureCoreWebView2Async(null);
            //设定虚拟域名
            webView.CoreWebView2.SetVirtualHostNameToFolderMapping("appassets.example", "", CoreWebView2HostResourceAccessKind.DenyCors);
            webView.Source = new Uri("https://appassets.example/index.html");

            webView.WebMessageReceived += WebView_WebMessageReceived;

            //禁止鼠标右键菜单
            await webView.CoreWebView2.ExecuteScriptAsync("window.addEventListener('contextmenu', window => {window.preventDefault();});");
            //禁止鼠标左键拖动选择
            await webView.CoreWebView2.ExecuteScriptAsync("window.addEventListener('selectstart', window => {window.preventDefault();});");
            //禁止拖动文件到窗口
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                   "window.addEventListener('dragover',function(e){e.preventDefault();},false);" +
                   "window.addEventListener('drop',function(e){" +
                      "e.preventDefault();" +
                      "console.log(e.dataTransfer);" +
                      "console.log(e.dataTransfer.files[0])" +
                   "}, false);");
        }

        void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = JSON.Parse(args.WebMessageAsJson);
            string fileName = DateTime.Now.ToString("yyMMdd_HH_mm_ss_") + json["width"].ToString() + "x" + json["height"].ToString();
            if (json["act"] == "save_video")
            {
                Base64Extract(json["data"], fileName + ".mp4");
            }
            else if(json["act"] == "save_img")
            {
                Base64Extract(json["data"], fileName + ".jpg");

            }
        }

        public static void Base64Extract(string inputText, string fileName)
        {
            string pattern = "";
            if (fileName.Substring(fileName.Length - 3, 3) == "mp4")
            {
                //匹配base64视频的正则表达式  格式类似 data:video/webm;codecs=h264;base64,GkXfo6NChoEBQveBAULygQRC84EIQoKIbWF0cm9za2FCh4EEQoWBAhhTgGcB/////////
                pattern = @"data:video/(?<type>.+?);codecs=h264;base64,(?<data>[^""]+)";
            }
            else if(fileName.Substring(fileName.Length - 3, 3) == "jpg")
            {
                //匹配所有base64格式图片的正则表达式
                pattern = @"data:image/(?<type>.+?);base64,(?<data>[^""]+)";
            }

            Regex regex = new Regex(pattern, RegexOptions.Compiled);
            MatchCollection matches = regex.Matches(inputText);
            int index = 0;
            foreach (Match match in matches)
            {
                string type = match.Groups["type"].Value;
                string data = match.Groups["data"].Value;
                byte[] bytes = Convert.FromBase64String(data);
                if (!Directory.Exists("_SampleWebview"))
                {
                    Directory.CreateDirectory("_SampleWebview");
                }


                //string fileName = "image_" + index.ToString() + "." + type;
                using (FileStream fs = new FileStream("_SampleWebview/" + fileName, FileMode.Create))
                {
                    fs.Write(bytes, 0, bytes.Length);
                }
                index++;
            }
        }


    }
}
