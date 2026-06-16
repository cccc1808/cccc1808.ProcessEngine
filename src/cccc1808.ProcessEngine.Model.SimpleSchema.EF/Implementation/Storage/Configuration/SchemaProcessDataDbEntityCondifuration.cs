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
    public class SchemaProcessDataDbEntityCondifuration<TId>
        : IEntityTypeConfiguration<SchemaProcessDataDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<SchemaProcessDataDbEntity<TId>> builder)
        {
            builder.Property(e => e.RootTriggerKey)
                .HasMaxLength(255);

            builder.Property(e => e.CurrentTokenId)
                .HasMaxLength(64);

            builder.HasIndex(e => e.ProcessId)
                .IsUnique();
        }
    }
}
