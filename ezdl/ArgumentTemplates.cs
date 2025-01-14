using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ezdl
{
    internal static class ArgumentTemplates
    {
        public static readonly string Default = "--compat-options embed-metadata,no-clean-infojson,no-keep-subs --force-ipv4 -ciw -o \"{OutputPath}/video.%(ext)s\" --check-formats --socket-timeout 2 --parse-metadata \"title:%(meta_title)s\" --parse-metadata \"uploader:%(meta_artist)s\" --parse-metadata \"%(channel_id)s:%(meta_channel_id)s\" --write-info-json --add-metadata --merge-output-format mkv {Url}";

        public static readonly string Facebook = "--compat-options embed-metadata,no-clean-infojson,no-keep-subs --force-ipv4 -ciw -o \"{OutputPath}/%(title).64s [%(id)s].%(ext)s\" --windows-filenames --restrict-filenames --add-metadata --merge-output-format mkv {Url}";

        public static readonly string YouTube = "--compat-options embed-metadata,no-clean-infojson,no-keep-subs {Cookies} --force-ipv4 -ciw -o \"{OutputPath}/video.%(ext)s\" -o \"thumbnail:{OutputPath}/thumbnail.%(ext)s\" {Format} --check-formats --socket-timeout 2 --parse-metadata \"title:%(meta_title)s\" --parse-metadata \"uploader:%(meta_artist)s\" --parse-metadata \"%(channel_id)s:%(meta_channel_id)s\" --write-info-json --add-metadata --write-sub --embed-subs --all-subs --convert-subs=srt --write-thumbnail --merge-output-format mkv {Comments} {ExtractorArgs} {Url}";

        public static readonly string YoutubeFormatAny = "-f \"((bestvideo{Height})+(bestaudio))/best\"";
        public static readonly string YoutubeFormatWebm = "-f \"((bestvideo[vcodec^=vp]{Height})+(bestaudio[acodec=opus]/bestaudio))/best\"";
        public static readonly string YoutubeFormatMp4 = "-f \"((bestvideo[vcodec^=avc]{Height})+(bestaudio[acodec^=mp4a]))/best[ext=mp4]{Height}\"";

        public static readonly string YoutubeExtractorArgs = "--extractor-args \"youtube:{0}\"";
        public static readonly string YoutubeCommentsAll = "max_comments=all,all,all,all;comment_sort=top";
        public static readonly string YoutubeCommentsLimited = "max_comments=2000,100,all,100;comment_sort=top";
        public static readonly string YoutubePoToken = "player-client=web,default;po_token=web+{0}";
    }
}
