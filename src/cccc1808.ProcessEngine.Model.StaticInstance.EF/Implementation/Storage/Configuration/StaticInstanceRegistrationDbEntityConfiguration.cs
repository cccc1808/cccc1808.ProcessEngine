using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Implementation.Storage.Configuration
{
    public class StaticInstanceRegistrationDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<StaticInstanceRegistrationDbEntity<TId>>
    {
        public string Name { get; set; }
            = "static_instance_registration";

        public void Configure(
            EntityTypeBuilder<StaticInstanceRegistrationDbEntity<TId>> builder)
        {
            builder.ToTable(Name);

            builder.Property(e => e.InstanceKey)
                .HasMaxLength(255);

            builder
                .HasIndex(e => new { e.ProcessType, e.InstanceKey })
                .IsUnique();

            builder.HasIndex(e => e.ProcessId)
                .IsUnique();
        }
    }
}
