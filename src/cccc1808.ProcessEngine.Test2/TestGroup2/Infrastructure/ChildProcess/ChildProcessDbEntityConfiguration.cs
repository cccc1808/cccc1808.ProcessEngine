using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.ChildProcess
{
    internal class ChildProcessDbEntityConfiguration
        : IEntityTypeConfiguration<ChildProcessDbEntity>
    {
        public void Configure(EntityTypeBuilder<ChildProcessDbEntity> builder)
        {
            builder.Property(e => e.ParentTriggerKey)
                .HasMaxLength(255);

            builder.HasIndex(e => e.ProcessId)
                .IsUnique();

            builder.HasIndex(e => e.ParentProcessId);
            builder.HasIndex(e => e.ActiveParentProcessId);
        }
    }
}
