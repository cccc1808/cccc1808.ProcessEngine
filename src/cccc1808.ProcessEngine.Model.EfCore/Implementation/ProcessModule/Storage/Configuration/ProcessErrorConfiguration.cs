using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Configuration
{
    public class ProcessErrorConfiguration<TId>
        : IEntityTypeConfiguration<ProcessErrorDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<ProcessErrorDbEntity<TId>> builder)
        {
            builder.HasOne(e => e.Process)
                .WithOne(e => e.Error)
                .HasForeignKey<ProcessErrorDbEntity<TId>>(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            ProcessIdIndex(builder);
        }

        /// <summary>
        /// <see cref="IProcessErrorDbEntityConditions{TId}.ProcessLinkedDbEntity"/>
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        protected IndexBuilder<ProcessErrorDbEntity<TId>> ProcessIdIndex(EntityTypeBuilder<ProcessErrorDbEntity<TId>> builder) 
        {
            return builder.HasIndex(e => e.ProcessId)
                .IsUnique();
        }
    }
}
