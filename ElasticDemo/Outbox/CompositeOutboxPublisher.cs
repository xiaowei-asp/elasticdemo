using Outbox.Abstractions;
using Outbox.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Outbox
{
    public sealed class CompositeOutboxPublisher : IOutboxPublishPipeline
    {
        private readonly IEnumerable<IOutboxPublisher> _publishers;

        public CompositeOutboxPublisher(IEnumerable<IOutboxPublisher> publishers)
        {
            _publishers = publishers;
        }

        public async Task PublishAsync(OutboxMessage message, CancellationToken ct)
        {
            foreach (var publisher in _publishers)
            {
                await publisher.PublishAsync(message, ct);
            }
        }
    }

}
