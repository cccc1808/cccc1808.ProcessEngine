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
    public class TimerProcessDbEntityConfiguration<TId>
        : ProcessDbEntityConfiguration<TId, TimerProcessDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<TimerProcessDbEntity<TId>> builder)
        {
            builder.HasIndex(e => new { e.Priority, e.ProcessTypeId, e.ProcessVersion, e.TimerDate, e.SelectLock })
            // include
                .HasFilter("AsyncExecute is true");

        }
    }
}
