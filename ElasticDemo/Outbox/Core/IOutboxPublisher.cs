using Outbox.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.Abstractions
{
    public interface IOutboxPublisher
    {
        Task PublishAsync(
            OutboxMessage message,
            CancellationToken ct = default
        );
    }
}
