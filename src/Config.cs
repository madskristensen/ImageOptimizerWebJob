using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ImageOptimizerWebJob
{
    public class Config
    {
        private const string _fileName = "compressor.json";

        public Config()
        {
            Includes = new List<string> { ConfigurationManager.AppSettings.Get("folderToWatch") };
            Excludes = new List<string> { "node_modules", "bower_components", "jspm_packages" };
            LogFilePath = ConfigurationManager.AppSettings.Get("logfile");
            FilePath = Path.Combine(ConfigurationManager.AppSettings.Get("folderToWatch"), _fileName);
        }

        public IEnumerable<string> Includes { get; set; }
        public IEnumerable<string> Excludes { get; set; }
        public bool Lossy { get; set; }

        [ScriptIgnore]
        public string LogFilePath { get; set; }

        [ScriptIgnore]
        public string FilePath { get; private set; }

        public static Config FromPath(string folder, string logFilePath)
        {
            var dir = new DirectoryInfo(folder);

            while (dir != null)
            {
                string configFile = Path.Combine(dir.FullName, _fileName);

                if (File.Exists(configFile))
                {
                    Console.WriteLine($"Read config from {configFile}");

                    using (var reader = new StreamReader(configFile))
                    {
                        var ser = new JavaScriptSerializer();
                        var options = ser.Deserialize<Config>(reader.ReadToEnd());
                        options.FilePath = configFile;
                        options.LogFilePath = logFilePath;
                        options.NormalizePaths();
                        return options;
                    }
                }

                dir = dir.Parent;
            }

            return new Config { LogFilePath = logFilePath };
        }

        private void NormalizePaths()
        {
            Includes = Includes.Select(pattern => CleanGlobbingPattern(pattern));
            Excludes = Excludes.Select(pattern => CleanGlobbingPattern(pattern));
            LogFilePath = new FileInfo(LogFilePath).FullName;
        }

        private string CleanGlobbingPattern(string pattern)
        {
            var dir = Path.GetDirectoryName(FilePath);
            string path = Path.Combine(dir, pattern).Replace('/', '\\').Replace(".\\", "");

            if (path.EndsWith("\\"))
            {
                path += "**\\*";
            }

            return path;
        }
    }
}
