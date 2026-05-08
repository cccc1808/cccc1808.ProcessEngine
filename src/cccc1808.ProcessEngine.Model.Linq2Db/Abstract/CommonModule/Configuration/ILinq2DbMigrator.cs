using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration
{
    public interface ILinq2DbMigrator
    {
        /// <summary>
        /// Необходимо вызвать 1 раз в начале в отдельном DI scope.
        /// </summary>
        void ConfigureSchema();


        Task MigrateAsync(CancellationToken cancellationToken);
    }
}
