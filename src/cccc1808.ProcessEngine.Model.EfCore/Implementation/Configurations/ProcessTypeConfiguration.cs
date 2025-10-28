using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Configurations
{
    internal class ProcessTypeConfiguration
        : IEntityTypeConfiguration<ProcessTypeEntity>
    {
        public void Configure(EntityTypeBuilder<ProcessTypeEntity> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasIndex(e => new { e.Name, e.Version })
                .IsUnique();

            builder.HasData(
                new ProcessTypeEntity() 
                {
                    Id = -1,
                    Name = "Retry timer",
                    Version = 1,
                }
                );
        }
    }
}
