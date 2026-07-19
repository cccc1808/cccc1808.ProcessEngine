using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Configuration
{
    public class TriggerEventOffsetInboxDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<TriggerEventOffsetInboxDbEntity<TId>>
    {
        public void Configure(
            EntityTypeBuilder<TriggerEventOffsetInboxDbEntity<TId>> builder)
        {
            builder.Property(e => e.QueueName)
                .HasMaxLength(255);

            UniqueIndex(builder);
        }

        public virtual IndexBuilder<TriggerEventOffsetInboxDbEntity<TId>> UniqueIndex(
            EntityTypeBuilder<TriggerEventOffsetInboxDbEntity<TId>> builder)
        {
            return builder.HasIndex(e => new { e.QueueName, e.PartitionId })
                .IsUnique();
        }
    }
}
