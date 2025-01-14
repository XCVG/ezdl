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

            Directory.CreateDirectory(tempPath);

            //parse args
            int resolution = 1080;
            int resArgIdx = Array.IndexOf(args, "-res");
            if (resArgIdx >= 0)
            {
                string resStr = args[resArgIdx + 1];
                if (resStr.Equals("max", StringComparison.OrdinalIgnoreCase))
                    resolution = -1;
                else
                    resolution = int.Parse(resStr);
            }

            logger.Info("max resolution: " + resolution);

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

            PreferredFormat preferredFormat = outputFormat == OutputFormat.Mp4 ? PreferredFormat.Mp4H264 : PreferredFormat.Unspecified; //ofmt mp4 implies fmt mp4, but can be overriden
            int fmtArgIdx = Array.IndexOf(args, "-fmt");
            if (fmtArgIdx >= 0)
            {
                string fmtString = args[fmtArgIdx + 1];
                if (fmtString.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                {
                    preferredFormat = PreferredFormat.Mp4H264;
                }
                else if (fmtString.Equals("vp9", StringComparison.OrdinalIgnoreCase))
                {
                    preferredFormat = PreferredFormat.WebmVp9;
                }
                else if (fmtString.Equals("any", StringComparison.OrdinalIgnoreCase))
                {
                    preferredFormat = PreferredFormat.Unspecified;
                }
            }            

            bool copyInfoJson = false;
            int copyInfoIdx = Array.IndexOf(args, "-info");
            if(copyInfoIdx >= 0)
            {
                copyInfoJson = true;
            }

            bool copyThumbnail = false;
            int copyThumbIdx = Array.IndexOf(args, "-thumb");
            if (copyThumbIdx >= 0)
            {
                copyThumbnail = true;
            }

            CommentsHandling commentsHandling = CommentsHandling.None;
            int commentsArgIdx = Array.IndexOf(args, "-comments");
            if (commentsArgIdx >= 0)
            {
                string commentsString = args[commentsArgIdx + 1];
                if (commentsString.Equals("all", StringComparison.OrdinalIgnoreCase))
                    commentsHandling = CommentsHandling.All;
                else
                    commentsHandling = CommentsHandling.Limited; //just specifying "comments" defaults to limited
            }

            bool useAltExe = false;
            int useAltExeIdx = Array.IndexOf(args, "-wo");
            if (useAltExeIdx >= 0)
            {
                useAltExe = true;
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

            string poToken = null;
            int poTokenIdx = Array.IndexOf(args, "-poToken");

            if(poTokenIdx >= 0)
            {
                poToken = args[poTokenIdx + 1].Trim().Trim('\'', '"').Trim();
            }
            else
            {
                string poTokenPath = null;
                string tPoTokenPath = Path.Combine(currentPath, "potoken.txt");
                if (File.Exists(tPoTokenPath))
                {
                    poTokenPath = tPoTokenPath;
                }
                else
                {
                    tPoTokenPath = Path.Combine(dataPath, "potoken.txt");
                    if (File.Exists(tPoTokenPath))
                    {
                        poTokenPath = tPoTokenPath;
                    }
                    else
                    {
                        tPoTokenPath = Path.Combine(applicationPath, "potoken.txt");
                        if (File.Exists(tPoTokenPath))
                        {
                            poTokenPath = tPoTokenPath;
                        }
                    }
                }

                if (poTokenPath != null)
                {
                    poTokenPath = Path.GetFullPath(poTokenPath);
                    logger.Info("using po toekn from file: " + poTokenPath);
                    poToken = File.ReadAllText(poTokenPath);
                }
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
            else if (uri.Host.Contains("twitter", StringComparison.OrdinalIgnoreCase) || uri.Host.StartsWith("x.", StringComparison.OrdinalIgnoreCase))
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
            else if (uri.Host.Contains("facebook", StringComparison.OrdinalIgnoreCase))
            {
                site = Site.Facebook;
            }

            logger.Info("Site: " + site);
            logger.Info("ID: " + (id == null ? "unknown" : id));

            var downloaderConfig = new DownloaderConfig()
            {
                CookiesFile = cookiesPath,
                PoToken = poToken,
                Url = urlString,
                Uri = uri,
                OutputFolder = currentPath,
                TempFolder = tempPath,
                OutputFormat = outputFormat,
                MaxResolution = resolution,
                PreferredFormat = preferredFormat,
                Comments = commentsHandling,
                CopyInfo = copyInfoJson,
                CopyThumbnail = copyThumbnail,
                Site = site,
                Id = id,
                DownloaderExe = useAltExe ? "yt-dlp-wo" : null
            };
            var downloader = new Downloader(downloaderConfig);

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

            downloader.ClearTempFolder();

            logger.Info("ezdl done!");
        }
    }
}
