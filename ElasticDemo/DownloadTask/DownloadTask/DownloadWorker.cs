using DownloadTask.FileStorage;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask
{
    public sealed class DownloadWorker : BackgroundService
    {
        private readonly IDownloadQueue _queue;
        private readonly IFileGenerator _generator;
        private readonly IFileStorage _storage;

        public DownloadWorker(
            IDownloadQueue queue,
            IFileGenerator generator,
            IFileStorage storage)
        {
            _queue = queue;
            _generator = generator;
            _storage = storage;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            await foreach (var cmd in _queue.DequeueAsync(stoppingToken))
            {
                var stream = await _generator.GenerateAsync(cmd, stoppingToken);
                var url = await _storage.SaveAsync(
                    cmd.FileName, stream, stoppingToken);

                // TODO: 保存结果（DB / Cache / 回调通知）
                Console.WriteLine($"Task:{cmd.TaskId}, Url:{url}");
            }
        }
    }

}
