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
        private static string[] _ignorePatterns = { "\\node_modules\\", "\\bower_components\\", "\\typings\\" };

        FileSystemWatcher _watcher;
        FileHashStore _store;

        public ImageQueue(string folder, string logFilePath)
        {
            foreach (string ext in _extensions)
            {
                var images = Directory.EnumerateFiles(folder, "*" + ext, SearchOption.AllDirectories);
                AddRange(images);
            }

            _store = new FileHashStore(logFilePath);
            StartListening(folder);
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

        public async Task ProcessQueueAsync(bool lossy)
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

                    if (_store.HasChangedOrIsNew(file))
                    {
                        Compressing?.Invoke(this, file);
                        var result = c.CompressFile(file, lossy);
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

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            string file = e.FullPath.ToLowerInvariant();
            string ext = Path.GetExtension(file);

            if (!string.IsNullOrEmpty(ext) &&
                !_ignorePatterns.Any(p => file.Contains(p)) &&
                _extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                if (_store.HasChangedOrIsNew(file))
                {
                    if (!Contains(file))
                    {
                        Add(file);
                    }
                }
            }
        }
    }
}