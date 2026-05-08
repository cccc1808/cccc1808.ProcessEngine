using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Configuration
{
    public class ProcessDbEntityInitMigration<TId> : ILinq2DbMigration
    {
        public async Task MigrateAsync(
            DataConnection dataConnection, 
            CancellationToken cancellationToken)
        {
            await dataConnection.CreateTableAsync<ProcessDbEntity<TId>>();

            // TODO: index.
            await DbProcessingForSelectorIndex(dataConnection);
            await DbProcessingForSelectorHandlerIndex(dataConnection);
            await AsyncExecuteIndex(dataConnection);
            await WaitEventIndex(dataConnection);
            await MaybeStoppedByTriggerEventLoosedIndex(dataConnection);
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.DbProcessingForSelector"/>
        /// </summary>
        /// <returns></returns>
        protected virtual async Task DbProcessingForSelectorIndex(DataConnection dataConnection)
        {
            //return builder.HasIndex(e => new { e.Priority, e.ProcessTypeId, e.ProcessVersion, e.SelectLockTimeout })
            //    .HasFilter($"status = {(int)ProcessStatusEnum.AsyncExecute}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.DbProcessingForHandler"/>
        /// </summary>
        /// <returns></returns>
        protected virtual async Task DbProcessingForSelectorHandlerIndex(DataConnection dataConnection)
        {
            //return builder.HasIndex(e => new { e.ProcessTypeId, e.ProcessVersion, e.Priority, e.Id })
            //    .HasFilter($"status = {(int)ProcessStatusEnum.AsyncExecute}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.AsyncExecute"/>
        /// </summary>
        protected virtual async Task AsyncExecuteIndex(DataConnection dataConnection)
        {
            //return builder.HasIndex(e => new { e.Id })
            //    .HasFilter($"status = {(int)ProcessStatusEnum.AsyncExecute}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.WaitEvent"/>
        /// </summary>
        protected virtual async Task WaitEventIndex(DataConnection dataConnection)
        {
            //return builder.HasIndex(e => new { e.Id })
            //    .HasFilter($"status = {(int)ProcessStatusEnum.WaitEvent}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.MaybeStoppedByTriggerEventLoosed"/>
        /// </summary>
        protected virtual async Task MaybeStoppedByTriggerEventLoosedIndex(DataConnection dataConnection)
        {
//            return builder.HasIndex(e => new { e.Id, e.SelectLockTimeout })
//                .HasFilter(@$"
//status = {(int)ProcessStatusEnum.WaitEvent}
//and stopped_by_error is false
//and retry_count is null");
        }
    }
}
