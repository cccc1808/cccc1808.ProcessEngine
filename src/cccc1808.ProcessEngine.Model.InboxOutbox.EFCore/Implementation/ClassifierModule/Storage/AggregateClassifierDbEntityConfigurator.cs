using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.ClassifierModule.Storage
{
    public class AggregateClassifierDbEntityConfigurator<TId>
        : IEntityTypeConfiguration<AggregateClassifierDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<AggregateClassifierDbEntity<TId>> builder)
        {
            // TODO:
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.AggregateType)
                .HasMaxLength(255);

            builder.Property(e => e.AggregateId)
                .HasMaxLength(50);

            AggregateIdAggregateTypeIndex(builder);
        }

        /// <summary>
        /// TODO: комменатрий
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void AggregateIdAggregateTypeIndex(EntityTypeBuilder<AggregateClassifierDbEntity<TId>> builder) 
        {
            builder.HasIndex(e => new { e.AggregateId, e.AggregateType })
                .IsUnique();
        }
    }
}
