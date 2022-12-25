﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ezdl
{
    internal static class ArgumentTemplates
    {
        public static readonly string Default = "--compat-options youtube-dl --force-ipv4 -ciw -o \"{OutputPath}/video.%(ext)s\" --parse-metadata \"title:%(meta_title)s\" --parse-metadata \"uploader:%(meta_artist)s\" --parse-metadata \"%(channel_id)s:%(meta_channel_id)s\" --write-info-json --add-metadata --merge-output-format mkv {Url}";

        public static readonly string YouTube = "--compat-options youtube-dl {Cookies} --force-ipv4 -ciw -o \"{OutputPath}/video.%(ext)s\" -o \"thumbnail:{OutputPath}/thumbnail.%(ext)s\" {Format} --parse-metadata \"title:%(meta_title)s\" --parse-metadata \"uploader:%(meta_artist)s\" --parse-metadata \"%(channel_id)s:%(meta_channel_id)s\" --write-info-json --add-metadata --write-sub --embed-subs --all-subs --convert-subs=srt --write-thumbnail --merge-output-format mkv {Comments} {Url}";

        public static readonly string YoutubeFormatWebm = "-f \"((bestvideo[vcodec=vp9]{Height})+(bestaudio[acodec=opus]/bestaudio))/best\"";
        public static readonly string YoutubeFormatMp4 = "-f \"((bestvideo[vcodec^=avc]{Height})+(bestaudio[acodec^=mp4a]))/best[ext=mp4]{Height}\"";

        public static readonly string YoutubeCommentsAll = "--write-comments --extractor-args \"youtube:max_comments=all,all,all,all;comment_sort=top\"";
        public static readonly string YoutubeCommentsLimited = "--write-comments --extractor-args \"youtube:max_comments=2000,100,all,100;comment_sort=top\"";
    }
}
