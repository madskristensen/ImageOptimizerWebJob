using Minimatch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImageOptimizerWebJob
{
    public class ImageQueue : List<string>
    {
        private static string[] _extensions = { ".jpg", ".jpeg", ".gif", ".png" };

        private FileSystemWatcher _watcher;
        private FileHashStore _store;
        private Config _config;
        private Minimatch.Options _matcherOptions = new Options { AllowWindowsPaths = true, IgnoreCase = true };

        public ImageQueue(Config config)
        {
            string dir = Path.GetDirectoryName(config.FilePath);

            foreach (string ext in _extensions)
            {
                var images = Directory.EnumerateFiles(dir, "*" + ext, SearchOption.AllDirectories);
                AddRange(images);
            }

            _store = new FileHashStore(config.LogFilePath);
            _config = config;
            StartListening(dir);
        }

        public event EventHandler<CompressionResult> Compressed;
        public event EventHandler<string> Compressing;

        private void StartListening(string folder)
        {
            _watcher = new FileSystemWatcher(folder);
            _watcher.Changed += FileChanged;
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.CreationTime;
            _watcher.EnableRaisingEvents = true;
        }

        public async Task ProcessQueueAsync()
        {
            Compressor c = new Compressor();

            int processors = Math.Max(Environment.ProcessorCount - 1, 1);
            var options = new ParallelOptions { MaxDegreeOfParallelism = processors };

            while (true)
            {
                var max = Count;

                Parallel.For(0, max, options, i =>
                {
                    var file = this[i];

                    if (IsImageOnProbingPath(file) && _store.HasChangedOrIsNew(file))
                    {
                        Compressing?.Invoke(this, file);
                        var result = c.CompressFile(file, _config.Lossy);
                        Compressed?.Invoke(this, result);
                        _store.Save(file);
                    }
                });

                if (max > 0)
                {
                    RemoveRange(0, max);
                }

                await Task.Delay(1000);
            }
        }

        private bool IsImageOnProbingPath(string file)
        {
            bool isIncluded = _config.Includes.Any(pattern => Minimatcher.Check(file, pattern, _matcherOptions));

            if (!isIncluded)
                return false;

            bool isExcluded = _config.Excludes.Any(pattern => Minimatcher.Check(file, pattern, _matcherOptions));

            if (isExcluded)
                return false;

            return true;
        }

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            string file = e.FullPath.ToLowerInvariant();
            string ext = Path.GetExtension(file);

            if (!string.IsNullOrEmpty(ext) && _extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                if (_store.HasChangedOrIsNew(file) && !Contains(file))
                {
                    Add(file);
                }
            }
        }
    }
}