using Outbox.Abstractions;
using Outbox.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Outbox
{
    /// <summary>
    /// 并行发布 要求下游完全幂等
    /// </summary>
    public sealed class ParallelOutboxPublisher : IOutboxPublishPipeline
    {
        private readonly IEnumerable<IOutboxPublisher> _publishers;

        public ParallelOutboxPublisher(IEnumerable<IOutboxPublisher> publishers)
        {
            _publishers = publishers;
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
        {
            var tasks = _publishers
                .Select(p => p.PublishAsync(message, ct));

            await Task.WhenAll(tasks);
        }
    }

}
