using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    public class OutboxProcessDataConfigurator<TId>
        : IEntityTypeConfiguration<OutboxProcessDataDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<OutboxProcessDataDbEntity<TId>> builder)
        {
            // TODO:
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.WakeupTriggerKey)
                .HasMaxLength(255);

            ProcessIdIndex(builder);
            AggregateQueueIndex(builder);
        }

        /// <summary>
        /// <see cref="IProcessLinkedConditions{TId, TEntity}.ProcessId"/>.
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void ProcessIdIndex(EntityTypeBuilder<OutboxProcessDataDbEntity<TId>> builder) 
        {
            builder.HasIndex(e => e.ProcessId)
                .IsUnique();            
        }

        /// <summary>
        /// TODO: see condtion
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void AggregateQueueIndex(EntityTypeBuilder<OutboxProcessDataDbEntity<TId>> builder)
        {
            builder.HasIndex(e => new { e.AggregateId, e.QueueId })
                .IsUnique();
        }
    }
}
