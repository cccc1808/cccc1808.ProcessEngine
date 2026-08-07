using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query
{
    public class EFQueueProcessRunnerQuery<TId> 
        : IQueueProcessRunnerQuery<TId>
    {
        private readonly IEFDbContext _dbContext;

        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;

        public EFQueueProcessRunnerQuery(
            IEFDbContext dbContext, 
            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions)
        {
            _dbContext = dbContext;
            _processDbEntityConditions = processDbEntityConditions;
        }

        public IQueueProcessRunnerQuery<TId>.IContext InitContext(
            IQueueProcessRunnerQuery<TId>.IOptions options, 
            ProcessRegistryDto processType
            )
        {
            if (options is not Options typedOptions)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return new Context()
            {
                Options = typedOptions,
                ProcessRegistry = processType,
                CurrentOffsetId = typedOptions.OffsetStartId,
            };
        }

        public async Task<ICollection<IQueueProcessRunnerQuery<TId>.SelectResult>> ExecuteAsync(
            IQueueProcessRunnerQuery<TId>.IContext context,
            CancellationToken cancellationToken)
        {
            if (context is not Context typedContext) 
            {
                throw new ArgumentException(nameof(context));
            }

            var ids = await _dbContext.Set<ProcessDbEntity<TId>>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.DbProcessingForSelector.Query,
                    new IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>.DbProcessingForSelectorParameters(
                        typedContext.ProcessRegistry,
                        typedContext.CurrentOffsetId)
                    )
                .Take(typedContext.Options.BatchSize)
                .Select(e => e.Id)
                .ToArrayAsync(cancellationToken);

            var result = new List<IQueueProcessRunnerQuery<TId>.SelectResult>(ids.Length);
            foreach (var elem in ids) 
            {
                result.Add(
                    new IQueueProcessRunnerQuery<TId>.SelectResult(
                        elem, 
                        typedContext.ProcessRegistry.Unique.ProcessType,
                        typedContext.ProcessRegistry.Unique.Priority
                        )
                    );

                if (Comparer<TId>.Default.Compare(typedContext.CurrentOffsetId, elem) > 0)
                {
                    typedContext.CurrentOffsetId = elem;
                }
            }

            return result;
        }

        #region

        public class Options 
            : IQueueProcessRunnerQuery<TId>.IOptions
        {
            public int BatchSize { get; set; } 
                = 100;

            public required TId OffsetStartId { get; set; }
        }

        private class Context 
            : IQueueProcessRunnerQuery<TId>.IContext
        {
            public required Options Options { get; init; }

            public required ProcessRegistryDto ProcessRegistry { get; init; }

            public required TId CurrentOffsetId { get; set; }
        }

        #endregion
    }
}
