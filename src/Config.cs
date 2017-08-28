using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace ImageOptimizerWebJob
{
    public class Config
    {
        public Config()
        {
            Optimizations = new List<Optimization> { new Optimization() };
            WarmupTime = Defaults.WarmupTime;
            CacheFilePath = Defaults.CacheFilePath;
            FilePath = Path.Combine(Defaults.FolderToWatch, Defaults.ConfigFileName);
        }

        public List<Optimization> Optimizations { get; set; }
        public int WarmupTime { get; set; }

        [ScriptIgnore]
        public string CacheFilePath { get; set; }

        [ScriptIgnore]
        public string LogFilePath { get; set; }

        [ScriptIgnore]
        public string FilePath { get; private set; }

        public static Config FromPath(string folder, string cacheFilePath)
        {
            var dir = new DirectoryInfo(folder);

            var config = new Config
            {
                CacheFilePath = cacheFilePath,
                FilePath = Path.Combine(dir.FullName, Defaults.ConfigFileName)
            };

            config.Update();

            return config;
        }

        public void Update()
        {
            if (File.Exists(FilePath))
            {
                Console.WriteLine($"Read config from {FilePath}");

                using (var reader = new StreamReader(FilePath))
                {
                    var ser = new JavaScriptSerializer();
                    var options = ser.Deserialize<Config>(reader.ReadToEnd());
                    Optimizations = options.Optimizations;
                    WarmupTime = options.WarmupTime;
                }
            }
            else
            {
                Console.WriteLine($"No config file present. Using default configuration");

                var options = new Config();
                Optimizations = options.Optimizations;
                WarmupTime = options.WarmupTime;
            }

            NormalizePaths();
        }

        private void NormalizePaths()
        {
            foreach (var opti in Optimizations)
            {
                opti.Includes = opti.Includes.Select(pattern => CleanGlobbingPattern(pattern));
                opti.Excludes = opti.Excludes.Select(pattern => CleanGlobbingPattern(pattern));
            }

            CacheFilePath = new FileInfo(CacheFilePath).FullName;
            LogFilePath = Path.ChangeExtension(CacheFilePath, ".log");
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

    public class Optimization
    {
        public IEnumerable<string> Includes { get; set; } = Defaults.Includes;
        public IEnumerable<string> Excludes { get; set; } = Defaults.Excludes;
        public bool Lossy { get; set; }
    }
}
