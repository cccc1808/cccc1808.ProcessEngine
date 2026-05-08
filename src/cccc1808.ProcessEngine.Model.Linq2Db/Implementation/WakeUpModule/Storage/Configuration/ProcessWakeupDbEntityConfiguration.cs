using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB.Mapping;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.Storage.Configuration
{
    public class ProcessWakeupDbEntityConfiguration<TId>
        : ILinq2DbConfigurator<ProcessWakeupDbEntity<TId>>
    {
        public virtual void Configure(FluentMappingBuilder mappingBuilder)
        {
            Configure(mappingBuilder.Entity<ProcessWakeupDbEntity<TId>>());
        }

        public virtual void Configure(EntityMappingBuilder<ProcessWakeupDbEntity<TId>> builder)
        {
            builder
                .HasIdentity(e => e.Id)
                .HasPrimaryKey(e => e.Id)
                .Property(e => e.Id).HasSkipOnInsert(false)
                .Property(e => e.ProcessId).HasSkipOnUpdate()
                .Association(e => e.Process, e => e.ProcessId, e => e.Id, canBeNull: true);
        }
    }
}
