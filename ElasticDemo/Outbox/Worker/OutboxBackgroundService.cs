using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Outbox.Abstractions;
using Outbox.Core;
using Outbox.Models;

namespace Outbox.Worker
{
    public class OutboxBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public OutboxBackgroundService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOnce(stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
        }

        private async Task ProcessOnce(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();

            var messages = await db.Set<OutboxMessage>()
                .Where(x =>
                    x.Status == OutboxStatus.Pending &&
                    (x.NextRetryTime == null || x.NextRetryTime <= DateTime.UtcNow))
                .OrderBy(x => x.CreatedTime)
                .Take(50)
                .ToListAsync(ct);

            foreach (var msg in messages)
            {
                await HandleMessage(scope, db, publisher, msg, ct);
            }
        }

        private static async Task HandleMessage(IServiceScope scope,
            DbContext db,
            IOutboxPublisher publisher,
            OutboxMessage msg,
            CancellationToken ct)
        {
            try
            {
                msg.Status = OutboxStatus.Processing;
                msg.UpdatedTime = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);

                var pipeline = scope.ServiceProvider
                    .GetRequiredService<IOutboxPublishPipeline>();

                await pipeline.PublishAsync(msg, ct);
                //await publisher.PublishAsync(msg, ct);

                msg.Status = OutboxStatus.Completed;
            }
            catch
            {
                msg.RetryCount++;

                msg.Status = msg.RetryCount >= 5
                    ? OutboxStatus.Failed
                    : OutboxStatus.Pending;

                msg.NextRetryTime = DateTime.UtcNow.AddMinutes(2);
            }

            msg.UpdatedTime = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }
}
