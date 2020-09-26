using Microsoft.Extensions.DependencyInjection;
using Outbox.Abstractions;
using Outbox.Core;
using Outbox.Publishers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddOutboxScope(this IServiceCollection services)
        {
            services.AddScoped<IOutboxPublisher, HttpOutboxPublisher>();
            services.AddScoped<IOutboxPublisher, MqOutboxPublisher>();

            services.AddScoped<IOutboxPublishPipeline, CompositeOutboxPublisher>();

            //想并行？
            //services.AddScoped<IOutboxPublishPipeline, ParallelOutboxPublisher>();
        }

        public static void AddOutboxSingleton(this IServiceCollection services)
        {
            services.AddSingleton<IOutboxPublisher, HttpOutboxPublisher>();
            services.AddSingleton<IOutboxPublisher, MqOutboxPublisher>();

            //
            services.AddSingleton<IOutboxPublishPipeline, CompositeOutboxPublisher>();

            //想并行？
            //services.AddSingleton<IOutboxPublishPipeline, ParallelOutboxPublisher>();
        }
    }
}
