using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.TriggersModule
{
    public class PostgresTriggerLockDbEntityConfiguration<TId> 
        : TriggerReserveDbEntityConfiguration<TId>
    {
        public override void Configure(EntityTypeBuilder<TriggerReserveDbEntity<TId>> builder)
        {
            base.Configure(builder);
            
            builder.IsUnlogged(true);

            // TODO
            // ALTER TABLE TriggerLockDbEntity SET TABLESPACE inmemory-tablespace;
        }
    }
}
