using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.TriggersModule.Storage.Configuration
{
    public class TriggerDbEntityInitMigration<TId> : ILinq2DbMigration
    {
        public async Task MigrateAsync(
            DataConnection dataConnection, 
            CancellationToken cancellationToken)
        {
            await dataConnection.CreateTableAsync<TriggerDbEntity<TId>>();

            // TODO: index.
            await KeyIndex(dataConnection, cancellationToken);
            await KeyNotCompleteIndex(dataConnection, cancellationToken);
            await DbProcessingForSelectorIndex(dataConnection, cancellationToken);
            await DbProcessingForHandlerParameters(dataConnection, cancellationToken);
            await ProcessIdIndex(dataConnection, cancellationToken);
            // await StreamDataProperty(builder);
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.Key"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual async Task KeyIndex(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            // Уникальный
            //return builder.HasIndex(e => e.Key)
            //    .IsUnique();
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.KeyAndNotComplete"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual async Task KeyNotCompleteIndex(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            //return builder
            //    .HasIndex(e => e.Key)
            //    .HasFilter("is_completed is false");
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.DbProcessingForSelector"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual async Task DbProcessingForSelectorIndex(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            // Для выборки DbWorker. selector
    //        return builder.HasIndex(e => new { e.Priority, e.TimerDate, e.SelectLockTimeout })
    //            .HasFilter(@"
    //is_activated is true 
    //and is_completed is false");
        }

        /// <summary>
        /// <see cref="ITriggerDbEntityConditions{TId}.DbProcessingForHandler"/>
        /// </summary>
        /// <param name="builder"></param>
        protected virtual async Task DbProcessingForHandlerParameters(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            // Для выборки DbWorker. handler executor.
    //        return builder.HasIndex(e => new { e.TimerDate, e.Id })
    //            .HasFilter(@"
    //is_activated is true
    //and is_completed is false");
        }

        protected virtual async Task ProcessIdIndex(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            // Вроде не используется, но пусть будет (может понадобится).
            //return builder.HasIndex(e => e.ProcessId);
        }


        //protected virtual PropertyBuilder<JsonElement?> StreamDataProperty(EntityTypeBuilder<TriggerDbEntity<TId>> builder) 
        //{
        //    builder.Ignore(e => e.SimpleStreamState);
        //    builder.Ignore(e => e.OffsetStreamState);

        //    return builder.Property(e => e.StreamData);
        //}
    }
}
