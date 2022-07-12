using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ezdl
{
    internal static class Utils
    {
        public static string RemuxAndCopy(string source, string destination, bool keepOriginal, IDictionary<string, string> tags)
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

    }
}
