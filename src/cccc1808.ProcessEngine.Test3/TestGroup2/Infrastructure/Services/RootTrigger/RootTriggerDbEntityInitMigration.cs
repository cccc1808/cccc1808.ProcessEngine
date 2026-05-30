using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;
using cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services.RootTrigger;

using LinqToDB;
using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    internal class RootTriggerDbEntityInitMigration
        : ILinq2DbMigration
    {
        public async Task MigrateAsync(DataConnection dataConnection, CancellationToken cancellationToken)
        {
            await dataConnection.CreateTableAsync<RootTriggerDbEntity>();

            // TODO: index.
            //builder.HasIndex(e => e.ParentProcessId);
            //builder.HasIndex(e => e.ActiveParentProcessId);

            //builder.HasIndex(e => e.ProcessId)
            //    .IsUnique();

            //builder.HasIndex(e => e.ParentProcessId);
            //builder.HasIndex(e => e.ActiveParentProcessId);
        }
    }
}
