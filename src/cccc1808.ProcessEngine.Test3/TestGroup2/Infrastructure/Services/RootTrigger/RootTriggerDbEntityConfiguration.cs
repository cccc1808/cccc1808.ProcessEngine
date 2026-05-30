using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;
using cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services.RootTrigger;

using LinqToDB.Mapping;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    internal class RootTriggerDbEntityConfiguration
        : ILinq2DbConfigurator<RootTriggerDbEntity>
    {
        public void Configure(FluentMappingBuilder mappingBuilder)
        {
            Configure(mappingBuilder.Entity<RootTriggerDbEntity>());
        }

        public void Configure(EntityMappingBuilder<RootTriggerDbEntity> builder)
        {
            builder
                .HasIdentity(e => e.Id)
                .HasPrimaryKey(e => e.Id)
                .Property(e => e.Id).HasSkipOnInsert(false);
        }        
    }
}
