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
    public class EFQueueMessageDbEntityConfiguration<TId> 
        : IEntityTypeConfiguration<EFQueueMessageDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<EFQueueMessageDbEntity<TId>> builder)
        {
            builder
                .Property(e => e.Key)
                .HasMaxLength(255);

            builder.HasIndex(e => e.QueuePartitionId);
        }
    }
}
