using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Configuration;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Postgres.Implementation.TriggersModule
{
    public class PostgresTriggerDbEntityConfiguration<TId>
        : TriggerDbEntityConfiguration<TId>
    {
        protected override IndexBuilder<TriggerDbEntity<TId>> DbProcessingForSelectorIndex(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            return base.DbProcessingForSelectorIndex(builder)
                .IncludeProperties(e => new { e.Id, e.HandlerKey });
        }

        protected override IndexBuilder<TriggerDbEntity<TId>> DbProcessingForSelectorIndex2(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            return base.DbProcessingForSelectorIndex2(builder)
                .IncludeProperties(e => new { e.Id });
        }

        protected override IndexBuilder<TriggerDbEntity<TId>> DbProcessingForSelectorIndex31(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            return base.DbProcessingForSelectorIndex2(builder)
                .IncludeProperties(e => new { e.Id });
        }

        protected override IndexBuilder<TriggerDbEntity<TId>> DbProcessingForSelectorIndex32(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        {
            return base.DbProcessingForSelectorIndex2(builder)
                .IncludeProperties(e => new { e.Id });
        }

        //protected override PropertyBuilder<JsonElement?> StreamDataProperty(EntityTypeBuilder<TriggerDbEntity<TId>> builder)
        //{
        //    return base.StreamDataProperty(builder)
        //        .HasColumnType("json");
        //}
    }
}
