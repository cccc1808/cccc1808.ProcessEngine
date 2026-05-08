using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LinqToDB.Data;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration
{
    public interface ILinq2DbMigration
    {
        Task MigrateAsync(
            DataConnection dataConnection,
            CancellationToken cancellationToken);
    }
}
