using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Storage
{
    internal class PrentChildProcessDbEntityConfiguration 
        : IEntityTypeConfiguration<ParentChildProcessDbEntity>
    {
        public void Configure(EntityTypeBuilder<ParentChildProcessDbEntity> builder)
        {
            builder.Property(e => e.TriggerKey)
                .HasMaxLength(255);

            builder.HasIndex(e => new { e.ProcessId, e.ChildProcessIndex });

            builder.HasIndex(e => e.ProcessId)
                .HasFilter("is_active = true");

            builder.HasIndex(e => e.ChildProcessId)
                .IsUnique();
        }
    }
}
