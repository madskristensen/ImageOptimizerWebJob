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
            string basePath = ConfigurationManager.AppSettings.Get("folderToWatch");
            string logFilePath = ConfigurationManager.AppSettings.Get("logfile");

            if (!Directory.Exists(basePath))
                basePath = "./";

            if (!Directory.Exists(Path.GetDirectoryName(logFilePath)))
                logFilePath = "log.txt";

            var options = Config.FromPath(basePath, logFilePath);
            Console.WriteLine($"Watching {new DirectoryInfo(basePath).FullName}");

            var queue = new ImageQueue(options);
            queue.Compressed += OnCompressed;

            Task.Run(async () =>
            {
                Console.WriteLine("Image Optimizer stared. Waiting for image file changes...");
                await queue.ProcessQueueAsync();

            }).GetAwaiter().GetResult();
        }
        private static void OnCompressed(object sender, CompressionResult e)
        {
            if (e.Saving > 0)
            {
                Console.WriteLine($"Optimized {Path.GetFileName(e.OriginalFileName)} by {e.Percent}%");
                File.Copy(e.ResultFileName, e.OriginalFileName, true);
                File.Delete(e.ResultFileName);
            }

        }
    }
}
