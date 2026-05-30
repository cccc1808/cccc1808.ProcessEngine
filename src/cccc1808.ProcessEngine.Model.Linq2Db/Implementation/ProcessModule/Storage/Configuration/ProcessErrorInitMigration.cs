using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Configuration
{
    public class ProcessErrorInitMigration<TId> : ILinq2DbMigration
    {
        public async Task MigrateAsync(DataConnection dataConnection, CancellationToken cancellationToken)
        {
            await dataConnection.CreateTableAsync<ProcessErrorDbEntity<TId>>(token: cancellationToken);

            // TODO: index.
            await ProcessIdIndex(dataConnection, cancellationToken);
        }

        /// <summary>
        /// <see cref="IProcessErrorDbEntityConditions{TId}.ProcessLinkedDbEntity"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        protected async Task ProcessIdIndex(DataConnection dataConnection, CancellationToken cancellationToken)
        {
            //return builder.HasIndex(e => e.ProcessId)
            //    .IsUnique();
        }
    }
}
