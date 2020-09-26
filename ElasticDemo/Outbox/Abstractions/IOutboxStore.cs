using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.Abstractions
{
    public interface IOutboxStore
    {
        Task AddAsync(
            string messageType,
            string aggregateId,
            object payload,
            CancellationToken ct = default
        );
    }
}
