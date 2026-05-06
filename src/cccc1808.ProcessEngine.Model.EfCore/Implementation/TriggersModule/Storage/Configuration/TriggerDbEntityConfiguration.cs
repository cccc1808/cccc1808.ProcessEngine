using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
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

            builder
                .Property(e => e.HandlerKey)
                .HasMaxLength(255);

            KeyIndex(builder);
            KeyNotCompleteIndex(builder);
            DbProcessingForSelectorIndex(builder);
            DbProcessingForHandlerParameters(builder);
            ProcessIdIndex(builder);
            // StreamDataProperty(builder);
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
                .HasFilter("is_completed is false");
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.DbProcessingForSelector"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual IndexBuilder<TriggerDbEntity<TId>> DbProcessingForSelectorIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        {
            // Для выборки DbWorker. selector
            return builder.HasIndex(e => new { e.Priority, e.TimerDate, e.SelectLockTimeout })
                .HasFilter(@"
    is_activated is true 
    and is_completed is false");
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
    is_activated is true
    and is_completed is false");
        }

        protected virtual IndexBuilder<TriggerDbEntity<TId>> ProcessIdIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        {
            // Вроде не используется, но пусть будет (может понадобится).
            return builder.HasIndex(e => e.ProcessId);
        }

        //protected virtual PropertyBuilder<JsonElement?> StreamDataProperty(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        //{
        //    builder.Ignore(e => e.SimpleStreamState);
        //    builder.Ignore(e => e.OffsetStreamState);

        //    return builder.Property(e => e.StreamData);
        //}
    }
}
