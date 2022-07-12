using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ezdl
{
    internal static class ArgumentTemplates
    {
        public static readonly string Default = "--compat-options youtube-dl -ciw -o \"{OutputPath}.%(ext)s\" --parse-metadata \"title:%(meta_title)s\" --parse-metadata \"uploader:%(meta_artist)s\" --parse-metadata \"%(channel_id)s:%(meta_channel_id)s\" --write-info-json --add-metadata --merge-output-format mkv {Url}";

        public static readonly string YouTube = "--compat-options youtube-dl {Cookies} -ciw -o \"{OutputPath}.%(ext)s\" {Format} --parse-metadata \"title:%(meta_title)s\" --parse-metadata \"uploader:%(meta_artist)s\" --parse-metadata \"%(channel_id)s:%(meta_channel_id)s\" --write-info-json --add-metadata --write-sub --embed-subs --all-subs --convert-subs=srt --embed-thumbnail --merge-output-format mkv {Url}";

        public static readonly string YoutubeFormatWebm = "-f \"((bestvideo[vcodec=vp9]{Height})+(bestaudio[acodec=opus]/bestaudio))/best\"";
        public static readonly string YoutubeFormatMp4 = "-f \"((bestvideo[vcodec^=avc]{Height})+(bestaudio[acodec^=mp4a]))/best[ext=mp4]{Height}\"";
    }
}
