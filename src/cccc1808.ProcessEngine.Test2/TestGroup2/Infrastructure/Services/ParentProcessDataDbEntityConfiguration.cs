using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services
{
    internal class ParentProcessDataDbEntityConfiguration
        : IEntityTypeConfiguration<ParentProcessDataDbEntity>
    {
        public void Configure(EntityTypeBuilder<ParentProcessDataDbEntity> builder)
        {
            builder.HasIndex(e => e.ProcessId)
                .IsUnique();
        }
    }
}
