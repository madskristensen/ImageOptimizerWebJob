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
        private static object _logRoot = new object();

        private FileSystemWatcher _watcher;
        private FileHashStore _store;
        private Config _config;
        private Options _matcherOptions = new Options { AllowWindowsPaths = true, IgnoreCase = true };

        public ImageQueue(Config config)
        {
            _store = new FileHashStore(config.CacheFilePath);
            _config = config;

            string dir = Path.GetDirectoryName(config.FilePath);
            QueueExistingFiles();
            StartListening(dir);
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

                    if (IsImageOnProbingPath(file) && _store.HasChangedOrIsNew(file, _config.Lossy))
                    {
                        var result = c.CompressFile(file, _config.Lossy);

                        HandleCompressionResult(result);

                        _store.Save(file, _config.Lossy);
                    }
                });

                if (max > 0)
                {
                    RemoveRange(0, max);
                }

                await Task.Delay(1000);
            }
        }

        private void HandleCompressionResult(CompressionResult result)
        {
            if (result.Saving > 0)
            {
                File.Copy(result.ResultFileName, result.OriginalFileName, true);
                File.Delete(result.ResultFileName);
            }

            string dir = Path.GetDirectoryName(_config.FilePath);

            lock (_logRoot)
            {
                using (var writer = new StreamWriter(_config.LogFilePath, true))
                {
                    string fileName = result.OriginalFileName.Replace(dir, string.Empty);
                    writer.WriteLine($"{DateTime.UtcNow.ToString("s")};{fileName};{result.Percent}%;{(_config.Lossy ? "lossy" : "lossless")}");
                }
            }
        }

        private void QueueExistingFiles()
        {
            string dir = Path.GetDirectoryName(_config.FilePath);

            foreach (string ext in _extensions)
            {
                var images = Directory.EnumerateFiles(dir, "*" + ext, SearchOption.AllDirectories);
                AddRange(images);
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
            string file = e.FullPath;
            string ext = Path.GetExtension(file);

            if (string.IsNullOrWhiteSpace(ext) || ext.Contains('~'))
                return;

            if (_extensions.Contains(ext, StringComparer.OrdinalIgnoreCase) && !Contains(file) && _store.HasChangedOrIsNew(file, _config.Lossy))
            {
                Add(file);
            }
            else if (e.FullPath == _config.FilePath)
            {
                _config.Update();
                QueueExistingFiles();
            }
        }
    }
}