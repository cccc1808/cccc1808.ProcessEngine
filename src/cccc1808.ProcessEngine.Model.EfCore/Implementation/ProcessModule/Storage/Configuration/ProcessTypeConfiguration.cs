using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Configuration
{
    internal class ProcessTypeConfiguration
        : IEntityTypeConfiguration<ProcessTypeEntity>
    {
        public void Configure(EntityTypeBuilder<ProcessTypeEntity> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasIndex(e => new { e.Name, e.Version })
                .IsUnique();
        }
    }
}
