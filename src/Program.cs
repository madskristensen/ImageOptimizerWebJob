using System;
using System.IO;
using System.Threading.Tasks;

namespace ImageOptimizerWebJob
{
    class Program
    {
        static void Main(string[] args)
        {
            string basePath = Defaults.FolderToWatch;
            string logFilePath = Defaults.CacheFilePath;

            if (!Directory.Exists(basePath))
                basePath = "./";

            if (!Directory.Exists(Path.GetDirectoryName(logFilePath)))
                logFilePath = "log.cache";

            var options = Config.FromPath(basePath, logFilePath);
            var queue = new ImageQueue(options);

            Task.Run(async () =>
            {
                int wait = options.WarmupTime * 1000;

                Console.WriteLine($"Image Optimizer starting. Waiting for {options.WarmupTime} seconds...");
                await Task.Delay(wait);

                Console.WriteLine("Image Optimizer started");
                Console.WriteLine($"Watching {new DirectoryInfo(basePath).FullName}");

                await queue.ProcessQueueAsync();

            }).GetAwaiter().GetResult();
        }
    }
}
