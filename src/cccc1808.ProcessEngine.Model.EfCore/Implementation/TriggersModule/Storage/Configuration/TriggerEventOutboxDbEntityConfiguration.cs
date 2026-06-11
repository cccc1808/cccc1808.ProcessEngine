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
    public class TriggerEventOutboxDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<TriggerEventOutboxDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<TriggerEventOutboxDbEntity<TId>> builder)
        {
            builder
                .HasIndex(e => e.Timestamp);
        }
    }
}
