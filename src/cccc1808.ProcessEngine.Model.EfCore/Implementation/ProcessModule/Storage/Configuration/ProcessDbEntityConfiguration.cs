using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Configuration
{
    public class ProcessDbEntityConfiguration<TId, TProcess>
        : IEntityTypeConfiguration<TProcess>
        where TProcess: ProcessDbEntity<TId>
    {
        public virtual void Configure(EntityTypeBuilder<TProcess> builder)
        {
            DbProcessingForSelectorIndex(builder);
            DbProcessingForSelectorHandlerIndex(builder);
            AsyncExecuteIndex(builder);
            MaybeStoppedByTriggerEventLoosedIndex(builder);
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.DbProcessingForSelector"/>
        /// </summary>
        /// <returns></returns>
        protected virtual IndexBuilder<TProcess> DbProcessingForSelectorIndex(EntityTypeBuilder<TProcess> builder)
        {
            return builder.HasIndex(e => new { e.Priority, e.ProcessTypeId, e.ProcessVersion, e.SelectLockTimeout })
                .HasFilter($"Status is {(int)ProcessStatusEnum.AsyncExecute}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.DbProcessingForHandler"/>
        /// </summary>
        /// <returns></returns>
        protected virtual IndexBuilder<TProcess> DbProcessingForSelectorHandlerIndex(EntityTypeBuilder<TProcess> builder)
        {
            return builder.HasIndex(e => new { e.ProcessTypeId, e.ProcessVersion, e.Id })
                .HasFilter($"Status is {(int)ProcessStatusEnum.AsyncExecute}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.AsyncExecute"/>
        /// </summary>
        protected virtual IndexBuilder<TProcess> AsyncExecuteIndex(EntityTypeBuilder<TProcess> builder) 
        {
            return builder.HasIndex(e => new { e.Id })
                .HasFilter($"Status is {(int)ProcessStatusEnum.AsyncExecute}");
        }

        /// <summary>
        /// <see cref="IProcessDbEntityConditions{TId, TEntity}.MaybeStoppedByTriggerEventLoosed"/>
        /// </summary>
        protected virtual IndexBuilder<TProcess> MaybeStoppedByTriggerEventLoosedIndex(EntityTypeBuilder<TProcess> builder) 
        {
            return builder.HasIndex(e => new { e.Id, e.SelectLockTimeout })
                .HasFilter(@$"
Status is {(int)ProcessStatusEnum.WaitEvent}
and StoppedByError is false
and RetryCount is null");
        }
    }
}
