using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Storage.Configuration
{
    public class SchemaDbEntityCondifuration<TId>
        : IEntityTypeConfiguration<SchemaDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<SchemaDbEntity<TId>> builder)
        {
            builder.Property(e => e.HandlerKey)
                .HasMaxLength(255);

            builder.HasIndex(e => new { e.ProcessTypeId, e.ProcessVersion })
                .IsUnique(true);
        }
    }
}
