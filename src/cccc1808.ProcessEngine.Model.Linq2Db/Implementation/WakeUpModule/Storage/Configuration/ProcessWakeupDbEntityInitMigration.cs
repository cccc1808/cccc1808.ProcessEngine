using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.Storage.Configuration
{
    public class ProcessWakeupDbEntityInitMigration<TId>
        : ILinq2DbMigration
    {
        public async Task MigrateAsync(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            await dataConnection.CreateTableAsync<ProcessWakeupDbEntity<TId>>();

            // TODO: index.
            await ProcessIdIndex(dataConnection, cancellationToken);
            await IsAsyncExecutingIndex(dataConnection, cancellationToken);
        }

        protected virtual async Task ProcessIdIndex(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            //return builder.HasIndex(e => e.ProcessId)
            //    .IsUnique();
        }

        /// <summary>
        /// <see cref="IProcessWakeupDbEntityConditions{TId}.IsAsyncExecuting"/>
        /// </summary>
        protected virtual async Task IsAsyncExecutingIndex(
            DataConnection dataConnection,
            CancellationToken cancellationToken)
        {
            //return builder.HasIndex(e => e.ProcessId)
            //    .HasFilter("is_async_executing is true")
            //    .IsUnique();
        }
    }
}
