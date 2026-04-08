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
    public class QueueClassifierDbEntityConfigurator<TId>
        : IEntityTypeConfiguration<QueueClassifierDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<QueueClassifierDbEntity<TId>> builder)
        {
            // TODO:
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Name)
                .HasMaxLength(255);

            NameIndex(builder);
        }

        /// <summary>
        /// TODO: комменатрий
        /// </summary>
        /// <param name="builder"></param>
        protected virtual void NameIndex(EntityTypeBuilder<QueueClassifierDbEntity<TId>> builder) 
        {
            builder.HasIndex(e => e.Name)
                .IsUnique();
        }
    }
}
