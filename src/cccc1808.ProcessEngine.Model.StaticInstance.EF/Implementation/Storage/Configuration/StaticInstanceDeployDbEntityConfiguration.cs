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
    public class StaticInstanceDeployDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<StaticInstanceDeployDbEntity<TId>>
    {
        public string Name { get; set; }
            = "static_instance_deploy";

        public void Configure(
            EntityTypeBuilder<StaticInstanceDeployDbEntity<TId>> builder)
        {
            builder.ToTable(Name);
        }
    }
}
