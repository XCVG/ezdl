using NLog;
using NLog.Layouts;
using System;
using System.IO;

namespace ezdl
{
    internal class Program
    {
        static void Main(string[] args)
        {
            //no config file (yet) but grab app folder to configure log
            string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ezdl");

            string logFilePath = Path.Combine(dataPath, "ezdl.log");
            NLog.LogManager.Setup().LoadConfiguration(builder => {
                builder.ForLogger().FilterMinLevel(LogLevel.Info).WriteToConsole(layout: Layout.FromString("${message}"));
                builder.ForLogger().FilterMinLevel(LogLevel.Debug).WriteToFile(fileName: logFilePath, archiveAboveSize: 1048576, maxArchiveFiles: 10);
            });
            var logger = NLog.LogManager.GetCurrentClassLogger();

            string tempPath = Path.Combine(dataPath, "temp");

            string currentPath = Directory.GetCurrentDirectory();
            string applicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            logger.Info($"ezdl started in {currentPath} with args {string.Join(',', args)}");

            Directory.Delete(tempPath, true);
            Directory.CreateDirectory(tempPath);

            //parse args
            int resolution = 1080;
            PreferredFormat preferredFormat = PreferredFormat.WebmVp9;

            int resArgIdx = Array.IndexOf(args, "-res");
            if(resArgIdx >= 0)
            {
                resolution = int.Parse(args[resArgIdx + 1]);
            }

            logger.Info("max resolution: " + resolution);

            int fmtArgIdx = Array.IndexOf(args, "-fmt");
            if (fmtArgIdx >= 0)
            {
                preferredFormat = (PreferredFormat)Enum.Parse(typeof(PreferredFormat), args[fmtArgIdx + 1]);
            }

            //ugly way of trying paths for cookies
            string cookiesPath = null;
            string tCookiesPath = Path.Combine(currentPath, "cookies.txt");
            if(File.Exists(tCookiesPath))
            {
                cookiesPath = tCookiesPath;
            }
            else
            {
                tCookiesPath = Path.Combine(dataPath, "cookies.txt");
                if (File.Exists(tCookiesPath))
                {
                    cookiesPath = tCookiesPath;
                }
                else
                {
                    tCookiesPath = Path.Combine(applicationPath, "cookies.txt");
                    if (File.Exists(tCookiesPath))
                    {
                        cookiesPath = tCookiesPath;
                    }
                }
            }

            if(cookiesPath != null)
            {
                cookiesPath = Path.GetFullPath(cookiesPath);
                logger.Info("using cookies file: " + cookiesPath);
            }

            var urlString = args[args.Length - 1].Trim('\'', '"');
            var uri = new Uri(urlString);

            logger.Info("URL: " + urlString);

            //TODO get ID and site

            var downloader = new Downloader(new DownloaderConfig()
            {
                CookiesFile = cookiesPath,
                Url = urlString,
                Uri = uri,
                OutputFolder = currentPath,
                TempFolder = tempPath,
                OutputFormat = OutputFormat.Mkv,
                MaxResolution = resolution,
                PreferredFormat = preferredFormat

            });
            downloader.Download();
        }
        
    }
}
