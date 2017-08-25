using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;

namespace ImageOptimizerWebJob
{
    class Program
    {
        static void Main(string[] args)
        {

            var dir = new DirectoryInfo(ConfigurationManager.AppSettings.Get("folderToWatch")).FullName;
            var logFilePath = new FileInfo(ConfigurationManager.AppSettings.Get("logfile")).FullName;

            if (!Directory.Exists(dir))
                dir = new DirectoryInfo("./").FullName;

            if (!Directory.Exists(Path.GetDirectoryName(logFilePath)))
                logFilePath = new FileInfo(Path.Combine(dir, "log.txt")).FullName;

            Console.WriteLine($"Watching {dir}");
            Console.WriteLine($"Logs to {logFilePath}");

            var queue = new ImageQueue(dir, logFilePath);
            queue.Compressing += OnCompressing;
            queue.Compressed += OnCompressed;

            Task.Run(async () =>
            {
                Console.WriteLine("Image Optimizer stared. Waiting for image file changes...");
                await queue.ProcessQueueAsync(false);

            }).GetAwaiter().GetResult();
        }

        private static void OnCompressing(object sender, string e)
        {
            Console.WriteLine($"Optimizing {Path.GetFileName(e)}...");
        }

        private static void OnCompressed(object sender, CompressionResult e)
        {
            if (e.Saving > 0)
            {
                Console.WriteLine($"Optimized {Path.GetFileName(e.OriginalFileName)} by {e.Percent}%");
                File.Copy(e.ResultFileName, e.OriginalFileName, true);
                File.Delete(e.ResultFileName);
            }
            else
            {
                Console.WriteLine($"{Path.GetFileName(e.OriginalFileName)} already optimized");
            }

        }
    }
}
