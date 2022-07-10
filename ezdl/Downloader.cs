using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            throw new NotImplementedException();

            //TODO port bits from elsewhere, need to handle long filenames and the other thing

            //TODO will return result path
        }
    }

    
}
