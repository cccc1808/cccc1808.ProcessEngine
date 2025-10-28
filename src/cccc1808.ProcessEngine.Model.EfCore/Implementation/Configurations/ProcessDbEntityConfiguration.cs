using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Configurations
{
    public class ProcessDbEntityConfiguration<TId, TProcess>
        : IEntityTypeConfiguration<TProcess>
        where TProcess: ProcessDbEntity<TId>
    {
        public void Configure(EntityTypeBuilder<TProcess> builder)
        {
            builder.HasIndex(e => new { e.Priority, e.ProcessTypeId, e.ProcessVersion, e.SelectLock })
            // include
                .HasFilter("AsyncExecute is true");

        }
    }
}
