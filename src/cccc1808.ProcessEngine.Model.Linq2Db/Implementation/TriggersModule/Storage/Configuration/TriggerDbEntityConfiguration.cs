using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB.Mapping;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.TriggersModule.Storage.Configuration
{
    public class TriggerDbEntityConfiguration<TId>
        : ILinq2DbConfigurator<TriggerDbEntity<TId>>
    {
        public void Configure(FluentMappingBuilder mappingBuilder)
        {
            Configure(mappingBuilder.Entity<TriggerDbEntity<TId>>());
        }

        public void Configure(EntityMappingBuilder<TriggerDbEntity<TId>> builder)
        {
            builder
                .HasIdentity(e => e.Id)
                .HasPrimaryKey(e => e.Id)
                .Property(e => e.Id).HasSkipOnInsert(false)
                .Property(e => e.Key).HasDataType(LinqToDB.DataType.NVarChar).HasLength(255).HasSkipOnUpdate()
                .Property(e => e.IsRangeHandler).HasSkipOnUpdate()
                .Property(e => e.HandlerKey).HasDataType(LinqToDB.DataType.NVarChar).HasLength(255).HasSkipOnUpdate()
                .Property(e => e.Priority).HasSkipOnUpdate()
                .Property(e => e.Kind).HasSkipOnUpdate()
                .Property(e => e.ProcessId).HasSkipOnUpdate();
        }      
    }
}
