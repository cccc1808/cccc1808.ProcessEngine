using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB.Mapping;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Configuration
{
    public class ProcessDbEntityConfiguration<TId, TProcess>
        : ILinq2DbConfigurator<TProcess>
        where TProcess : ProcessDbEntity<TId>
    {
        public virtual void Configure(FluentMappingBuilder mappingBuilder)
        {
            Configure(mappingBuilder.Entity<TProcess>());
        }

        public virtual void Configure(EntityMappingBuilder<TProcess> builder)
        {
            builder
                .HasIdentity(e => e.Id)
                .HasPrimaryKey(e => e.Id)
                .Property(e => e.Id).HasSkipOnInsert(false)
                .Property(e => e.ProcessTypeId).HasSkipOnUpdate()
                .Property(e => e.ProcessVersion).HasSkipOnUpdate()
                .Association(e => e.Error, e => e.Id, e => e.ProcessId, canBeNull: true);
        }        
    }
}
