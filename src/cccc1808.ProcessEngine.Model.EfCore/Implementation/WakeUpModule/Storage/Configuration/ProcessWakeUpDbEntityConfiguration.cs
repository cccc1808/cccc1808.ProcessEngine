using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.WakeupModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage.Configuration
{
    public class ProcessWakeUpDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<ProcessWakeupDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<ProcessWakeupDbEntity<TId>> builder)
        {
            builder.HasOne(e => e.Process)
                .WithOne()
                .HasForeignKey<ProcessWakeupDbEntity<TId>>(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            ProcessIdIndex(builder);
            IsAsyncExecutingIndex(builder);
        }

        protected virtual IndexBuilder<ProcessWakeupDbEntity<TId>> ProcessIdIndex(
            EntityTypeBuilder<ProcessWakeupDbEntity<TId>> builder)
        {
            return builder.HasIndex(e => e.ProcessId)
                .IsUnique();
        } 

        /// <summary>
        /// <see cref="IProcessWakeupDbEntityConditions{TId}.IsAsyncExecuting"/>
        /// </summary>
        protected virtual IndexBuilder<ProcessWakeupDbEntity<TId>> IsAsyncExecutingIndex(
            EntityTypeBuilder<ProcessWakeupDbEntity<TId>> builder)
        {
            return builder.HasIndex(e => e.ProcessId)
                .HasFilter("is_async_executing is true")
                .IsUnique();
        }
    }
}
