using DownloadTask.FileStorage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DownloadTask
{
    public static class DownloadServiceCollectionExtensions
    {
        public static IServiceCollection AddDownloadEngine(
            this IServiceCollection services,
            Action<DownloadOptions>? configure = null)
        {
            services.AddSingleton<IDownloadQueue, ChannelDownloadQueue>();
            services.AddSingleton<IFileGenerator, CsvFileGenerator>();
            services.AddSingleton<IFileStorage>(
                _ => new LocalFileStorage("downloads"));

            services.AddHostedService<DownloadWorker>();

            return services;
        }
    }

}
