using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;

namespace SampleCaptura
{
    public static class FFmpegService
    {
        const string FFmpegExeName = "ffmpeg.exe";

        //static FFmpegSettings GetSettings() => ServiceProvider.Get<FFmpegSettings>();

        public static bool FFmpegExists
        {
            get
            {
                var folderPath = "D:\\ffmpeg";// GetSettings().GetFolderPath();

                // FFmpeg folder
                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    var path = Path.Combine(folderPath, FFmpegExeName);

                    if (File.Exists(path))
                        return true;
                }

                if (File.Exists(FFmpegExeName))
                    return true;

                // PATH
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = FFmpegExeName,
                        Arguments = "-version",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    return true;
                }
                catch { return false; }
            }
        }

        public static string FFmpegExePath
        {
            get
            {
                var folderPath = "D:\\ffmpeg";// GetSettings().GetFolderPath();

                // FFmpeg folder
                if (!string.IsNullOrWhiteSpace(folderPath))
                {
                    var path = Path.Combine(folderPath, FFmpegExeName);

                    if (File.Exists(path))
                        return path;
                }

                //

                return System.Environment.CurrentDirectory + "\\ffmpeg";
            }
        }

        //Arguments
        //-thread_queue_size 512 -framerate 10 -f rawvideo -pix_fmt rgb32 -video_size 1280x720 -i \\.\pipe\captura-481413be-d71c-4207-bf8a-79d8af7223c4 -r 10 -vcodec libx264 -crf 15 -pix_fmt yuv420p -preset ultrafast "F:\WorkLian\TestOut\2023-04-07-18-26-36.mp4"
        public static Process StartFFmpeg(string Arguments, string FileName)//, out IFFmpegLogEntry FFmpegLog)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = FFmpegExePath,
                    Arguments = Arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = false,
                    RedirectStandardInput = true
                },
                EnableRaisingEvents = true
            };

            //方便起见，去掉了log信息
            //var log = ServiceProvider.Get<IFFmpegLogRepository>();

            //var logItem = log.CreateNew(Path.GetFileName(FileName), Arguments);
            //FFmpegLog = logItem;

            //process.ErrorDataReceived += null;

            process.Start();

            //process.BeginErrorReadLine();

            return process;
        }

        public static bool WaitForConnection(this NamedPipeServerStream ServerStream, int Timeout)
        {
            var asyncResult = ServerStream.BeginWaitForConnection(Ar => { }, null);

            if (asyncResult.AsyncWaitHandle.WaitOne(Timeout))
            {
                ServerStream.EndWaitForConnection(asyncResult);

                return ServerStream.IsConnected;
            }

            return false;
        }
    }
}

