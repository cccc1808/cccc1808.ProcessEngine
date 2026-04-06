using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeUpModule.Storage.Configuration
{
    public class ProcessWakeUpDbEntityConfiguration<TId>
        : IEntityTypeConfiguration<ProcessWakeUpDbEntity<TId>>
    {
        public void Configure(EntityTypeBuilder<ProcessWakeUpDbEntity<TId>> builder)
        {
            builder.HasOne(e => e.Process)
                .WithOne()
                .HasForeignKey<ProcessWakeUpDbEntity<TId>>(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            ProcessIdIndex(builder);
            IsAsyncExecutingIndex(builder);
        }

        protected virtual IndexBuilder<ProcessWakeUpDbEntity<TId>> ProcessIdIndex(
            EntityTypeBuilder<ProcessWakeUpDbEntity<TId>> builder)
        {
            return builder.HasIndex(e => e.ProcessId)
                .IsUnique();
        } 

        /// <summary>
        /// <see cref="IProcessWakeUpDbEntityConditions{TId}.IsAsyncExecuting"/>
        /// </summary>
        protected virtual IndexBuilder<ProcessWakeUpDbEntity<TId>> IsAsyncExecutingIndex(
            EntityTypeBuilder<ProcessWakeUpDbEntity<TId>> builder)
        {
            return builder.HasIndex(e => e.ProcessId)
                .HasFilter("is_async_executing is true")
                .IsUnique();
        }
    }
}
