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
        private string TempFileName;

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
            TempFileName = Guid.NewGuid().ToString();
            string tempFilePath = Path.Combine(Config.TempFolder, TempFileName);

            string argumentsTemplate = ArgumentTemplates.Default;
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            bool copyInfoJson = Config.CopyInfo;

            if(Config.Site == Site.Facebook)
            {
                argumentsTemplate = ArgumentTemplates.Facebook;
            }

            if(Config.Site == Site.YouTube)
            {
                argumentsTemplate = ArgumentTemplates.YouTube;
                List<string> extractorArgs = new List<string>();
                string resStr = Config.MaxResolution >= 0 ? $"[height<={Config.MaxResolution}]" : "";
                if (Config.PreferredFormat == PreferredFormat.Mp4H264)
                {
                    arguments["Format"] = Smart.Format(ArgumentTemplates.YoutubeFormatMp4, new { Height = resStr });
                }
                else if (Config.PreferredFormat == PreferredFormat.WebmVp9)
                {
                    arguments["Format"] = Smart.Format(ArgumentTemplates.YoutubeFormatWebm, new { Height = resStr });
                }
                else
                {
                    arguments["Format"] = Smart.Format(ArgumentTemplates.YoutubeFormatAny, new { Height = resStr });
                }

                if(Config.Comments == CommentsHandling.Limited)
                {
                    arguments["Comments"] = "--write-comments";
                    extractorArgs.Add(ArgumentTemplates.YoutubeCommentsLimited);
                    copyInfoJson = true;
                }
                else if (Config.Comments == CommentsHandling.All)
                {
                    arguments["Comments"] = "--write-comments";
                    extractorArgs.Add(ArgumentTemplates.YoutubeCommentsAll);
                    copyInfoJson = true;
                }
                else
                {
                    arguments["Comments"] = "";
                }

                if (!string.IsNullOrEmpty(Config.PoToken))
                {
                    extractorArgs.Add(string.Format(ArgumentTemplates.YoutubePoToken, Config.PoToken));
                }

                if (extractorArgs.Count > 0)
                {
                    arguments["ExtractorArgs"] = string.Format(ArgumentTemplates.YoutubeExtractorArgs, string.Join(';', extractorArgs));
                }
                else
                {
                    arguments["ExtractorArgs"] = "";
                }
            }

            Logger.Info($"Downloading from {Config.Url} to temp file {tempFilePath}");

            var dlTime = DateTime.UtcNow;
            DownloadVideoInternal(url, argumentsTemplate, tempFilePath, arguments);

            Thread.Sleep(100); //anti-glitching

            string infoFilePath = null;
            string id = "unknown", title = "unknown", thumbnailPath = "";
            string dlpResultPath;
            if (Config.Site == Site.Facebook)
            {
                //hack: avoid infojson, get the file in the temp file path, parse its filename to get title and ID
                string filePath = Directory.EnumerateFiles(tempFilePath).Single();
                string fileName = Path.GetFileNameWithoutExtension(filePath);

                title = fileName.Substring(0, fileName.IndexOf('[')).Trim();
                id = fileName.Substring(fileName.LastIndexOf('[') + 1, fileName.LastIndexOf(']') - fileName.LastIndexOf('[') - 1);

                dlpResultPath = filePath; 
            }
            else
            {
                infoFilePath = Path.Combine(tempFilePath, "video.info.json");
                if (!File.Exists(infoFilePath))
                {
                    throw new FileNotFoundException("No infojson produced, download probably failed!");
                }

                string infoRaw = File.ReadAllText(infoFilePath);
                JObject infoObject = JObject.Parse(infoRaw);

                dlpResultPath = infoObject["_filename"].ToString();                

                if (infoObject["title"] != null && infoObject["title"].ToString() != null)
                {
                    title = infoObject["title"].ToString();
                }

                if (infoObject["id"] != null && infoObject["id"].ToString() != null)
                {
                    id = infoObject["id"].ToString();
                }

                if (infoObject["thumbnail"] != null && infoObject["thumbnail"].ToString() != null && Directory.Exists(tempFilePath))
                {
                    var files = Directory.EnumerateFiles(tempFilePath);
                    foreach (var file in files)
                    {
                        if (Path.GetFileNameWithoutExtension(file).Equals("thumbnail", StringComparison.OrdinalIgnoreCase))
                        {
                            thumbnailPath = Path.GetFullPath(file);
                        }
                    }
                }
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
            tags.Add("EZDL_ORIGINAL_URL", Config.Url);

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
            RemuxAndCopy(dlpResultPath, finalFilePath, true, thumbnailPath, Config.OutputFormat, tags);

            if(copyInfoJson && !string.IsNullOrEmpty(infoFilePath))
            {
                string finalJsonPath = Path.Combine(Path.GetDirectoryName(finalFilePath), Path.GetFileNameWithoutExtension(finalFilePath) + ".json");
                Logger.Info($"Copying info JSON to {finalJsonPath}");
                File.Copy(infoFilePath, finalJsonPath);
            }

            if(Config.CopyThumbnail && !string.IsNullOrEmpty(thumbnailPath))
            {
                string finalThumbPath = Path.Combine(Path.GetDirectoryName(finalFilePath), Path.GetFileNameWithoutExtension(finalFilePath) + Path.GetExtension(thumbnailPath));
                Logger.Info($"Copying thumbnail to {finalThumbPath}");
                File.Copy(thumbnailPath, finalThumbPath);
            }

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
            p.StartInfo.FileName = string.IsNullOrEmpty(Config.DownloaderExe) ? "yt-dlp" : Config.DownloaderExe;
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

        public void ClearTempFolder()
        {
            if (string.IsNullOrEmpty(TempFileName))
                return;

            Logger.Info("Cleaning temp files");

            var files = Directory.EnumerateFiles(Config.TempFolder);
            foreach(var file in files)
            {
                if(Path.GetFileName(file).StartsWith(TempFileName, StringComparison.Ordinal))
                {
                    File.Delete(file);
                }
            }

            var folders = Directory.EnumerateDirectories(Config.TempFolder);
            foreach(var folder in folders)
            {
                if(folder.EndsWith(TempFileName, StringComparison.Ordinal))
                {
                    Directory.Delete(folder, true);
                }
            }

            Logger.Info("Cleaned temp files");
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

        private static string RemuxAndCopy(string source, string destination, bool keepOriginal, string thumbnailPath, OutputFormat outputFormat, IDictionary<string, string> tags)
        {
            for (int i = 1; File.Exists(destination); i++)
            {
                destination = Path.Combine(Path.GetDirectoryName(destination), Path.GetFileNameWithoutExtension(destination) + $" ({i})" + Path.GetExtension(destination));
            }

            source = Path.GetFullPath(source);
            destination = Path.GetFullPath(destination);

            string tagString = string.Join(" ", tags.Select(t => $"-metadata {t.Key}=\"{t.Value.Replace("\"", "\\\"")}\""));
            string mapString = outputFormat == OutputFormat.Mp4 ? "" : "-map 0";
            
            string attachString = string.Empty;
            if (outputFormat != OutputFormat.Mp4 && !string.IsNullOrEmpty(thumbnailPath) && File.Exists(thumbnailPath))
            {
                string extension = Path.GetExtension(thumbnailPath);
                string mimeType = MimeTypes.GetMimeType(Path.GetFileName(thumbnailPath));
                attachString = $"-attach \"{thumbnailPath}\" -metadata:s:t mimetype={mimeType} -metadata:s:t filename=cover{extension}";
            }

            using (Process p = new Process())
            {
                var pLogger = NLog.LogManager.GetLogger("ffmpeg");

                p.StartInfo.FileName = "ffmpeg";
                p.StartInfo.WorkingDirectory = Path.GetDirectoryName(source);
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.Arguments = $"-i \"{source}\" -c copy {mapString} {tagString} {attachString} \"{destination}\"";
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

                p.WaitForExit(30000);

                if (!p.HasExited)
                {
                    throw new Exception("ffmpeg took too long");
                }
            }

            Thread.Sleep(100); //make sure FS changes are committed

            if (!File.Exists(destination) || GetFileSize(destination) == 0)
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

        private static long GetFileSize(string filePath)
        {
            var fi = new FileInfo(filePath);
            return fi.Length;
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
