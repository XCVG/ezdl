using NLog;
using NLog.Layouts;
using System;
using System.IO;
using System.Web;

namespace ezdl
{
    internal class Program
    {
        static void Main(string[] args)
        {
            try
            {
                DownloadVideo(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Fatal error {e.GetType().Name}: {e.Message}");
            }
        }

        private static void DownloadVideo(string[] args)
        {
            //no config file (yet) but grab app folder to configure log
            string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ezdl");

            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            string logFilePath = Path.Combine(dataPath, "ezdl.log");
            NLog.LogManager.Setup().LoadConfiguration(builder =>
            {
                builder.ForLogger().FilterMinLevel(LogLevel.Info).WriteToConsole(layout: Layout.FromString("${message}"));
                builder.ForLogger().FilterMinLevel(LogLevel.Debug).WriteToFile(fileName: logFilePath, archiveAboveSize: 1048576, maxArchiveFiles: 10);
            });
            var logger = NLog.LogManager.GetCurrentClassLogger();

            string tempPath = Path.Combine(dataPath, "temp");

            string currentPath = Directory.GetCurrentDirectory();
            string applicationPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            logger.Info($"ezdl started in {currentPath} with args {string.Join(',', args)}");

            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            Directory.CreateDirectory(tempPath);

            //parse args
            int resolution = 1080;
            PreferredFormat preferredFormat = PreferredFormat.WebmVp9;

            int resArgIdx = Array.IndexOf(args, "-res");
            if (resArgIdx >= 0)
            {
                resolution = int.Parse(args[resArgIdx + 1]);
            }

            logger.Info("max resolution: " + resolution);

            int fmtArgIdx = Array.IndexOf(args, "-fmt");
            if (fmtArgIdx >= 0)
            {
                string fmtString = args[fmtArgIdx + 1];
                if (fmtString.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                {
                    preferredFormat = PreferredFormat.Mp4H264;
                }
            }

            OutputFormat outputFormat = OutputFormat.Mkv;
            int ofmtArgIdx = Array.IndexOf(args, "-ofmt");
            if (ofmtArgIdx >= 0)
            {
                string ofmtString = args[ofmtArgIdx + 1];
                if (ofmtString.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                {
                    outputFormat = OutputFormat.Mp4;
                }
            }

            //ugly way of trying paths for cookies
            string cookiesPath = null;
            string tCookiesPath = Path.Combine(currentPath, "cookies.txt");
            if (File.Exists(tCookiesPath))
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

            if (cookiesPath != null)
            {
                cookiesPath = Path.GetFullPath(cookiesPath);
                logger.Info("using cookies file: " + cookiesPath);
            }

            var urlString = args[args.Length - 1].Trim('\'', '"');
            var uri = new Uri(urlString);

            logger.Info("URL: " + urlString);

            string id = null;
            Site site = Site.Unknown;
            if (uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                site = Site.YouTube;
                var qs = HttpUtility.ParseQueryString(uri.Query);
                id = qs["v"];
            }
            else if (uri.Host.Contains("twitter", StringComparison.OrdinalIgnoreCase))
            {
                site = Site.Twitter;
                var statusSegmentIdx = Array.IndexOf(uri.Segments, "status/");
                id = uri.Segments[statusSegmentIdx + 1].TrimEnd('/');
            }
            else if (uri.Host.Contains("imgur", StringComparison.OrdinalIgnoreCase))
            {
                site = Site.Imgur;
                if (uri.Segments.Length > 0)
                {
                    id = uri.Segments[uri.Segments.Length - 1];
                }
            }
            else if (uri.Host.Contains("reddit", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith("redd.it", StringComparison.OrdinalIgnoreCase))
            {
                site = Site.Reddit;
            }

            logger.Info("Site: " + site);
            logger.Info("ID: " + (id == null ? "unknown" : id));

            var downloader = new Downloader(new DownloaderConfig()
            {
                CookiesFile = cookiesPath,
                Url = urlString,
                Uri = uri,
                OutputFolder = currentPath,
                TempFolder = tempPath,
                OutputFormat = outputFormat,
                MaxResolution = resolution,
                PreferredFormat = preferredFormat,
                Site = site,
                Id = id
            });

            string finalPath = null;
            try
            {
                finalPath = downloader.Download();
            }
            catch (Exception e)
            {
                logger.Error($"Failed to download ({e.GetType().Name}:{e.Message})");
            }

            if (!string.IsNullOrEmpty(finalPath))
            {
                logger.Info($"Downloaded {urlString} to {finalPath}");
            }
            else
            {
                logger.Error($"Did not download any file");
            }

            /*
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
            }
            */

            logger.Info("ezdl done!");
        }
    }
}
