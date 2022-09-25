using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ezdl
{
    public class DownloaderConfig
    {
        public int MaxResolution { get; set; } = 1080;
        public string OutputFolder { get; set; }
        public string TempFolder { get; set; }
        public string CookiesFile { get; set; } = null;
        public PreferredFormat PreferredFormat { get; set; }
        public OutputFormat OutputFormat { get; set; }
        public CommentsHandling Comments { get; set; }
        public bool CopyInfo { get; set; }
        public bool CopyThumbnail { get; set; }

        public string Url { get; set; }
        public Uri Uri { get; set; }
        public Site Site { get; set; }
        public string Id { get; set; }
    }

    public enum Site
    {
        Unknown, YouTube, Imgur, Twitter, Reddit
    }

    public enum PreferredFormat
    {
        Unspecified, WebmVp9, Mp4H264
    }

    public enum OutputFormat
    {
        Unspecified, AsInput, Mkv, Mp4
    }

    public enum CommentsHandling
    {
        None, All, Limited
    }
}
