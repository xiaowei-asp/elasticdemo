using System;
using System.Collections.Generic;
using System.Text;

namespace Outbox.Core
{
    public interface IMqProducer
    {
        Task PublishAsync(
            string exchange,
            string routingKey,
            string body,
            IDictionary<string, object>? headers = null,
            CancellationToken ct = default
        );
    }

}
