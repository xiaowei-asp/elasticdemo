using Outbox.Abstractions;
using Outbox.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Outbox.Publishers
{
    public sealed class MqOutboxPublisher : IOutboxPublisher
    {
        private readonly IMqProducer _producer;

        public MqOutboxPublisher(IMqProducer producer)
        {
            _producer = producer;
        }

        public Task PublishAsync(OutboxMessage message, CancellationToken ct)
        {
            return _producer.PublishAsync(
                exchange: "domain-events",
                routingKey: message.MessageType,
                body: message.Payload,
                headers: new Dictionary<string, object>
                {
                    ["aggregateId"] = message.AggregateId
                },
                ct);
        }
    }

}
