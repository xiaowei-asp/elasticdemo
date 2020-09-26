using Microsoft.EntityFrameworkCore;
using Outbox.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Outbox.EFCore
{
    public static class OutboxDbContextExtensions
    {
        public static void UseOutbox(this ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>(b =>
            {
                b.ToTable("OutboxMessage");
                b.HasKey(x => x.Id);
                b.Property(x => x.MessageType).IsRequired();
                b.Property(x => x.AggregateId).IsRequired();
                b.Property(x => x.Payload).IsRequired();
                b.Property(x => x.Status).IsRequired();
                b.HasIndex(x => x.Status);
                b.HasIndex(x => x.CreatedTime);
            });
        }
    }

}
