using System.Collections.Generic;
using System.Configuration;

namespace ImageOptimizerWebJob
{
    public static class Defaults
    {
        public static readonly string ConfigFileName = ConfigurationManager.AppSettings.Get("configFileName");
        public static readonly string CacheFilePath = ConfigurationManager.AppSettings.Get("logfile");
        public static readonly string FolderToWatch = ConfigurationManager.AppSettings.Get("folderToWatch");
        public static readonly int WarmupTime = 60;

        public static readonly List<string> Includes = new List<string> { FolderToWatch    };
        public static readonly List<string> Excludes = new List<string> { "node_modules", "bower_components", "jspm_packages" };    }
}
