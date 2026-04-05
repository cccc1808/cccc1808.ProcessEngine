using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.ProcessModule
{
    public class PostgresProcessDbEntityConfiguration<TId, TProcess>
        : ProcessDbEntityConfiguration<TId, TProcess>
        where TProcess : ProcessDbEntity<TId>
    {
        protected override IndexBuilder<TProcess> DbProcessingForSelectorIndex(EntityTypeBuilder<TProcess> builder)
        {
            return base.DbProcessingForSelectorIndex(builder)
                .IncludeProperties(e => e.Id!);
        }
    }
}
