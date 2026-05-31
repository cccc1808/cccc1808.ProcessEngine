using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.QueueModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.QueueModule.Storage.Configuration
{
    public class EFQueuePartitionDbEntityConfiguration<TId> 
        : IEntityTypeConfiguration<EFQueuePartitionDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<EFQueuePartitionDbEntity<TId>> builder)
        {
            builder
                .Property(e => e.TopicName)
                .HasMaxLength(255);

            builder.HasIndex(e => new { e.TopicName, e.PartitionId })
                .IsUnique(true);

            builder.HasIndex(e => new { e.TopicName, e.ProcessDate });
        }
    }
}
