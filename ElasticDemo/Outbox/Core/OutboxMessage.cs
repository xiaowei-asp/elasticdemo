using Outbox.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.Core
{
    public class OutboxMessage
    {
        public long Id { get; set; }

        public string MessageType { get; set; } = default!;

        public string AggregateId { get; set; } = default!;

        public string Payload { get; set; } = default!;

        public OutboxStatus Status { get; set; }

        public int RetryCount { get; set; }

        public DateTime? NextRetryTime { get; set; }

        public DateTime CreatedTime { get; set; }

        public DateTime UpdatedTime { get; set; }
    }
}
