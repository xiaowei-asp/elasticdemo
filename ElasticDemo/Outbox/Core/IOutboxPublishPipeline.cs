using System;
using System.Collections.Generic;
using System.Text;

namespace Outbox.Core
{
    public interface IOutboxPublishPipeline
    {
        Task PublishAsync(OutboxMessage message, CancellationToken ct);
    }
}
