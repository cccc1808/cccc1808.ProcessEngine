using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Storage
{
    public class OutboxMessageConfigurator<TId>
        : IEntityTypeConfiguration<OutboxMessageDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<OutboxMessageDbEntity<TId>> builder)
        {
            IdempotencyIndex(builder);
            IsActiveIndex(builder);
        }

        /// <summary>
        /// <see cref="IProcessLinkedConditions{TId, TEntity}.ProcessId"/> покрывает эту тему.
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void IdempotencyIndex(EntityTypeBuilder<OutboxMessageDbEntity<TId>> builder) 
        {
            builder.HasIndex(e => new { e.ProcessId, e.IdemporencyId })
                .IsUnique();
        }

        /// <summary>
        /// <see cref="IMessageStreamConditions{TId, TEntity}.IsActiveMessages"/>
        /// <see cref="IMessageStreamConditions{TId, TEntity}.ForProcessing"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void IsActiveIndex(EntityTypeBuilder<OutboxMessageDbEntity<TId>> builder)
        {
            builder.HasIndex(e => new { e.ProcessId, e.Priority, e.OrderId })
                .HasFilter("IsActive is true");
        }
    }
}
