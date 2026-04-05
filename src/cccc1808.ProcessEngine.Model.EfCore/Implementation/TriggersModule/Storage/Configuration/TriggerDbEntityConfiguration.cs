using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Configuration
{
    public class TriggerDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<TriggerDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            builder
                .Property(e => e.Key)
                .HasMaxLength(255);

            KeyIndex(builder);
            KeyNotCompleteIndex(builder);
            DbProcessingForSelectorIndex(builder);
            DbProcessingForHandlerParameters(builder);
            ProcessIdIndex(builder);            
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.Key"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual IndexBuilder<TriggerDbEntity<TId>> KeyIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        {
            // Уникальный
            return builder.HasIndex(e => e.Key)
                .IsUnique();
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.KeyAndNotComplete"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual IndexBuilder<TriggerDbEntity<TId>> KeyNotCompleteIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            return builder
                .HasIndex(e => e.Key)
                .HasFilter("IsCompleted is false");
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.DbProcessingForSelector"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual IndexBuilder<TriggerDbEntity<TId>> DbProcessingForSelectorIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        {
            // Для выборки DbWorker. selector
            return builder.HasIndex(e => new { e.Priority, e.TimerDate, e.SelectTimer })
                .HasFilter(@"
    IsActivated is true 
    and IsCompleted is false");
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.DbProcessingForHandler"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual IndexBuilder<TriggerDbEntity<TId>> DbProcessingForHandlerParameters(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            // Для выборки DbWorker. handler executor.
            return builder.HasIndex(e => new { e.TimerDate, e.Id })
                .HasFilter(@"
    IsActivated is true 
    and IsCompleted is false");
        }

        protected virtual IndexBuilder<TriggerDbEntity<TId>> ProcessIdIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        {
            // Вроде не используется, но пусть будет (может понадобится).
            return builder.HasIndex(e => e.ProcessId);
        }
    }
}
