using Newtonsoft.Json.Linq;
using NLog;
using SmartFormat;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ezdl
{
    internal class Downloader
    {
        DownloaderConfig Config;

        private ILogger Logger;

        public Downloader(DownloaderConfig config)
        {
            Config = config;
            Logger = NLog.LogManager.GetCurrentClassLogger();
        }

        public string Download()
        {
            string extension = ".mkv";
            if(Config.OutputFormat == OutputFormat.Mp4) //experimental and partially supported
            {
                extension = ".mp4";
            }

            string url = Config.Url;            
            string tempFileName = Guid.NewGuid().ToString();
            string tempFilePath = Path.Combine(Config.TempFolder, tempFileName);

            string argumentsTemplate = ArgumentTemplates.Default;
            Dictionary<string, string> arguments = new Dictionary<string, string>();

            if(Config.Site == Site.YouTube)
            {
                argumentsTemplate = ArgumentTemplates.YouTube;
                if(Config.PreferredFormat == PreferredFormat.Mp4H264)
                {
                    arguments["Format"] = Smart.Format(ArgumentTemplates.YoutubeFormatMp4, new { Height = $"[height<={Config.MaxResolution}]" });
                }
                else
                {
                    arguments["Format"] = Smart.Format(ArgumentTemplates.YoutubeFormatWebm, new { Height = $"[height<={Config.MaxResolution}]" });
                }
            }

            Logger.Info($"Downloading from {Config.Url} to temp file {tempFilePath}");

            var dlTime = DateTime.UtcNow;
            DownloadVideoInternal(url, argumentsTemplate, tempFilePath, arguments);            

            Thread.Sleep(100); //anti-glitching

            string infoFilePath = tempFilePath + ".info.json";
            if (!File.Exists(infoFilePath))
            {
                throw new FileNotFoundException("No infojson produced, download probably failed!");
            }

            string infoRaw = File.ReadAllText(infoFilePath);
            JObject infoObject = JObject.Parse(infoRaw);

            string dlpResultPath = infoObject["_filename"].ToString();
            string id = "unknown", title = "unknown";

            if (infoObject["title"] != null && infoObject["title"].ToString() != null)
            {
                title = infoObject["title"].ToString();
            }

            if (infoObject["id"] != null && infoObject["id"].ToString() != null)
            {
                id = infoObject["id"].ToString();
            }

            Dictionary<string, string> tags = new Dictionary<string, string>();
            if(Config.Site == Site.Imgur)
            {
                var imgurMetadata = GetMetadataImgur(Config.Id);
                
                title = imgurMetadata.Title;
                tags = imgurMetadata.Tags;

                tags.Add("IMGUR_DL_ID", id);

                id = Config.Id;
            }
            else
            {
                tags.Add("MTOOL_ID", id);
                tags.Add("MTOOL_SITE", Config.Site.ToString().ToLower(CultureInfo.InvariantCulture));
            }

            tags.Add("EZDL_RETRIEVAL_DATE", dlTime.ToString("O", CultureInfo.InvariantCulture));

            Logger.Info($"Title: {title}");
            string safeTitle = GetCleanTitle(title);
            Logger.Info($"Title (FS safe): {safeTitle}");
            string finalFileName = $"{safeTitle} - {id}{extension}"; //%(title)s - %(id)s.%(ext)s
            string finalFilePath = Path.Combine(Config.OutputFolder, finalFileName);

            for (int i = 1; File.Exists(finalFilePath); i++)
            {
                finalFilePath = Path.Combine(Path.GetDirectoryName(finalFilePath), Path.GetFileNameWithoutExtension(finalFilePath) + $" ({i})" + Path.GetExtension(finalFilePath));
            }

            Logger.Info($"Setting tags and copying to {finalFilePath}");
            RemuxAndCopy(dlpResultPath, finalFilePath, true, tags);

            //TODO save tempfilename as instance variable, make ClearTempFolder public, and call from outside
            Logger.Info($"Clearing temp folder");
            ClearTempFolder(tempFileName);

            return finalFilePath;
        }

        private void DownloadVideoInternal(string url, string argumentsTemplate, string outputPath, IDictionary<string, string> additionalArguments)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            arguments["Cookies"] = string.IsNullOrEmpty(Config.CookiesFile) ? "" : $"--cookies \"{Config.CookiesFile}\"";
            arguments["Height"] = $"[height<={Config.MaxResolution}]";

            arguments["OutputPath"] = outputPath.TrimEnd('/', '\\');

            arguments["Url"] = url;

            if(additionalArguments != null && additionalArguments.Count > 0)
            {
                foreach(var arg in additionalArguments)
                {
                    arguments[arg.Key] = arg.Value;
                }
            }            

            string argumentsString = Smart.Format(argumentsTemplate, arguments);

            var pLogger = NLog.LogManager.GetLogger("yt-dlp");

            Process p = new Process();
            p.StartInfo.FileName = "yt-dlp";
            p.StartInfo.Arguments = argumentsString;
            p.StartInfo.CreateNoWindow = false;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.OutputDataReceived += new DataReceivedEventHandler((s, e) =>
            {
                pLogger.Info(e.Data);
            }
            );
            p.ErrorDataReceived += new DataReceivedEventHandler((s, e) =>
            {
                pLogger.Error(e.Data);
            }
            );

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.WaitForExit();
        }

        private void ClearTempFolder(string baseFileName)
        {
            var files = Directory.EnumerateFiles(Config.TempFolder);
            foreach(var file in files)
            {
                if(Path.GetFileName(file).StartsWith(baseFileName, StringComparison.Ordinal))
                {
                    File.Delete(file);
                }
            }
        }

        private static RetrievedMetadata GetMetadataImgur(string id)
        {
            string metadataString = DownloadMetadataImgur($"https://imgur.com/gallery/{id}");
            if (metadataString == null)
            {
                //also try url of the format https://imgur.com/{id}
                metadataString = DownloadMetadataImgur($"https://imgur.com/{id}");
            }
            if (metadataString == null)
            {
                throw new Exception("can't find json data in response (probably missing)");
            }

            JObject metadataObject = JObject.Parse(metadataString);

            DateTime creationDate = DateTime.Parse(metadataObject["created_at"].ToString());

            var tags = new Dictionary<string, string>()
            {
                { "title", metadataObject["title"].ToString() },
                { "COMMENT", metadataObject["description"].ToString() },
                { "ARTIST", metadataObject["account"]["username"].ToString() },
                { "DATE", creationDate.Date.ToString("yyyyMMdd") },
                { "DESCRIPTION", metadataObject["description"].ToString() },
                { "PURL", metadataObject["url"].ToString() },
                { "UPLOADER_ID", metadataObject["account_id"].ToString() },
                { "MTOOL_ID", id},
                { "MTOOL_SITE", "imgur" },
                { "MTOOL_RAW_DATE", metadataObject["created_at"].ToString() }
            };

            return new RetrievedMetadata()
            {
                MetadataObject = metadataObject,
                MetadataString = metadataString,
                UploadDate = creationDate,
                Tags = tags,
                Title = metadataObject["title"].ToString()
            };
        }

        private static string DownloadMetadataImgur(string url) //TODO args
        {
            string htmlData = null;

            Task.Run(async () =>
            {
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("GET"), url))
                    {
                        var response = await httpClient.SendAsync(request);
                        htmlData = await response.Content.ReadAsStringAsync();
                    }
                }
            }).Wait();

            if (string.IsNullOrWhiteSpace(htmlData))
                return null;

            string matchPattern = "<script>[^\"]+\"{.*}\"<\\/script>";
            var match = Regex.Match(htmlData, matchPattern);
            if (!match.Success)
            {
                return null;
            }

            int startIndex = match.Value.IndexOf('{');
            int endIndex = match.Value.LastIndexOf('}') - startIndex + 1;
            string metadataString = match.Value.Substring(startIndex, endIndex).Replace("\\\"", "\"").Replace("\\\\", "\\");

            return metadataString;
        }

        private static string GetCleanTitle(string title)
        {
            var cleanTitle = string.Join("_", title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
            if (cleanTitle.Length > 128)
                cleanTitle = cleanTitle.Substring(0, 128);
            return cleanTitle;
        }

        private static string RemuxAndCopy(string source, string destination, bool keepOriginal, IDictionary<string, string> tags)
        {
            for (int i = 1; File.Exists(destination); i++)
            {
                destination = Path.Combine(Path.GetDirectoryName(destination), Path.GetFileNameWithoutExtension(destination) + $" ({i})" + Path.GetExtension(destination));
            }

            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);

            string tagString = string.Join(" ", tags.Select(t => $"-metadata {t.Key}=\"{t.Value.Replace("\"", "\\\"")}\""));

            using (Process p = new Process())
            {
                p.StartInfo.FileName = "ffmpeg";
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(source);
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                //p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.Arguments = $"-i \"{source}\" -c:v copy -c:a copy -c:s copy -map 0 {tagString} \"{destination}\"";

                p.Start();

                p.WaitForExit(30000);

                if (!p.HasExited)
                {
                    throw new Exception("ffmpeg took too long");
                }
            }

            Thread.Sleep(100); //make sure FS changes are committed

            if (!File.Exists(destination))
            {
                throw new FileNotFoundException("ffmpeg failed to create file");
            }

            var modifiedDate = File.GetLastWriteTime(source);
            File.SetLastWriteTime(destination, modifiedDate);

            if (!keepOriginal)
            {
                File.Delete(source);
            }

            Thread.Sleep(100); //make sure FS changes are committed

            return destination;
        }

        private class RetrievedMetadata
        {
            public string MetadataString;
            public JObject MetadataObject;
            public Dictionary<string, string> Tags;
            public string Title;
            public DateTime? UploadDate;
        }
    }

    
}
