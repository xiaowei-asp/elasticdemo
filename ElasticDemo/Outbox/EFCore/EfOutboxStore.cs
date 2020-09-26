using Microsoft.EntityFrameworkCore;
using Outbox.Abstractions;
using Outbox.Core;
using Outbox.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.EFCore
{
    public sealed class EfOutboxStore : IOutboxStore
    {
        private readonly DbContext _db;
        private readonly IOutboxSerializer _serializer;

        public EfOutboxStore(DbContext db, IOutboxSerializer serializer)
        {
            _db = db;
            _serializer = serializer;
        }

        public async Task AddAsync(
            string messageType,
            string aggregateId,
            object payload,
            CancellationToken ct = default)
        {
            var msg = new OutboxMessage
            {
                MessageType = messageType,
                AggregateId = aggregateId,
                Payload = _serializer.Serialize(payload),
                Status = OutboxStatus.Pending,
                CreatedTime = DateTime.UtcNow,
                UpdatedTime = DateTime.UtcNow
            };

            _db.Set<OutboxMessage>().Add(msg);
            await Task.CompletedTask;
        }
    }

}
