using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    internal class ParentProcessDataDbEntityInitMigration
        : ILinq2DbMigration
    {
        public async Task MigrateAsync(DataConnection dataConnection, CancellationToken cancellationToken)
        {
            await dataConnection.CreateTableAsync<ParentProcessDataDbEntity>();

            // TODO: index.
            //builder.HasIndex(e => e.ProcessId)
            //     .IsUnique();
        }
    }
}
