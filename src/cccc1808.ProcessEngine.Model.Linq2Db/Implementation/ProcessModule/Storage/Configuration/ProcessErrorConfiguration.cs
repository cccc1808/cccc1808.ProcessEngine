using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Configuration;

using LinqToDB.Mapping;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Configuration
{
    public class ProcessErrorConfiguration<TId>
        : ILinq2DbConfigurator<ProcessErrorDbEntity<TId>>
    {
        public void Configure(FluentMappingBuilder mappingBuilder)
        {
            Configure(mappingBuilder.Entity<ProcessErrorDbEntity<TId>>());
        }

        public void Configure(EntityMappingBuilder<ProcessErrorDbEntity<TId>> builder)
        {
            builder
                .HasIdentity(e => e.Id)
                .HasPrimaryKey(e => e.Id)
                .Property(e => e.Id).HasSkipOnInsert(false)
                .Property(e => e.ProcessId).HasSkipOnUpdate()
                .Property(e => e.Error)
                    .HasDataType(LinqToDB.DataType.Json)
                    .HasConversionFunc(
                        e => e?.GetRawText(),
                        e => 
                        {
                            if (e is null)
                            {
                                return null;
                            }
                            using (var document = JsonDocument.Parse(e))
                            {
                                return document.RootElement.Clone();
                            }
                        })
                .Association(e => e.Process, e => e.ProcessId, e => e.Id, canBeNull: true);
        }
    }
}
