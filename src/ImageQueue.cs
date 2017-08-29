using Minimatch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImageOptimizerWebJob
{
    public class ImageQueue : Dictionary<string, DateTime>
    {
        private static object _logRoot = new object();

        private FileSystemWatcher _watcher;
        private FileHashStore _cache;
        private Config _config;
        private Options _matcherOptions = new Options { AllowWindowsPaths = true, IgnoreCase = true };

        public ImageQueue(Config config)
        {
            _cache = new FileHashStore(config.CacheFilePath);
            _config = config;

            string dir = Path.GetDirectoryName(config.FilePath);
            StartListening(dir);
        }

        public async Task ProcessQueueAsync()
        {
            QueueExistingFiles();

            Compressor c = new Compressor();

            while (true)
            {
                var files = this.Where(e => e.Value < DateTime.Now.AddSeconds(-2))
                                .Select(e => e.Key)
                                .ToArray();

                foreach (string file in files)
                {
                    if (IsImageOnProbingPath(file, out var opti) && _cache.HasChangedOrIsNew(file))
                    {
                        var result = c.CompressFile(file, opti.Lossy);

                        HandleCompressionResult(result, opti);

                        _cache.Save(file);
                    }

                    Remove(file);
                }

                await Task.Delay(5000);
            }
        }

        private void HandleCompressionResult(CompressionResult result, Optimization opti)
        {
            if (result.Saving > 0)
            {
                DateTime creationTime = File.GetCreationTime(result.OriginalFileName);
                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                File.SetCreationTime(result.OriginalFileName, creationTime);
                File.Delete(result.ResultFileName);
            }

            string dir = Path.GetDirectoryName(_config.FilePath);
            string fileName = result.OriginalFileName.Replace(dir, string.Empty);
            double percent = Math.Max(result.Percent, 0);

            lock (_logRoot)
            {
                using (var writer = new StreamWriter(_config.LogFilePath, true))
                {
                    writer.WriteLine($"{DateTime.UtcNow.ToString("s")};{fileName};{percent}%;{(opti.Lossy ? "lossy" : "lossless")}");
                }
            }
        }

        private void QueueExistingFiles()
        {
            string dir = Path.GetDirectoryName(_config.FilePath);

            foreach (string ext in Defaults.Extensions)
            {
                var images = Directory.EnumerateFiles(dir, "*" + ext, SearchOption.AllDirectories);

                foreach (var image in images)
                {
                    this[image] = DateTime.Now;
                }
            }
        }

        private void StartListening(string folder)
        {
            _watcher = new FileSystemWatcher(folder);
            _watcher.Changed += FileChanged;
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.CreationTime;
            _watcher.EnableRaisingEvents = true;
        }

        private bool IsImageOnProbingPath(string file, out Optimization optimization)
        {
            optimization = null;

            foreach (var opti in _config.Optimizations)
            {
                optimization = opti;

                bool isIncluded = opti.Includes.Any(pattern => Minimatcher.Check(file, pattern, _matcherOptions));

                if (!isIncluded)
                    continue;

                bool isExcluded = opti.Excludes.Any(pattern => Minimatcher.Check(file, pattern, _matcherOptions));

                if (isExcluded)
                    continue;

                return true;
            }

            return false;
        }

        private async void FileChanged(object sender, FileSystemEventArgs e)
        {
            string file = e.FullPath;
            string ext = Path.GetExtension(file);

            if (string.IsNullOrWhiteSpace(ext) || ext.Contains('~'))
                return;

            if (Defaults.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase) && !ContainsKey(file) && _cache.HasChangedOrIsNew(file))
            {
                this[file] = DateTime.Now;
            }
            else if (e.FullPath == _config.FilePath)
            {
                await Task.Delay(2000).ConfigureAwait(false);
                _config.Update();
                QueueExistingFiles();
            }
        }
    }
}