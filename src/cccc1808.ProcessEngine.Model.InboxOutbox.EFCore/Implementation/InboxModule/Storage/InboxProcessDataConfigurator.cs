using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Storage
{
    public class InboxProcessDataConfigurator<TId>
        : IEntityTypeConfiguration<InboxProcessDataDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<InboxProcessDataDbEntity<TId>> builder)
        {
            builder.Property(e => e.WakeupTriggerKey)
                .HasMaxLength(255);

            ProcessIdIndex(builder);
        }

        /// <summary>
        /// <see cref="IProcessLinkedConditions{TId, TEntity}.ProcessId"/>.
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void ProcessIdIndex(EntityTypeBuilder<InboxProcessDataDbEntity<TId>> builder) 
        {
            builder.HasIndex(e => e.ProcessId)
                .IsUnique();            
        }
    }
}
