using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LinqToDB.Data;
using LinqToDB.Mapping;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration
{
    public interface ILinq2DbConfigurator
    {
        void Configure(FluentMappingBuilder mappingBuilder);
    }

    public interface ILinq2DbConfigurator<TEntity>
        : ILinq2DbConfigurator
    {
        void Configure(EntityMappingBuilder<TEntity> builder);
    }
}
