using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Storage.Query
{
    public class EFTriggerSelectQuery<TId>
        : ITriggerSelectQuery<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;        
        private readonly ITriggerDbEntityConditions<TId> _triggerDbEntityConditions;

        public EFTriggerSelectQuery(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,             
            ITriggerDbEntityConditions<TId> triggerDbEntityConditions)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _triggerDbEntityConditions = triggerDbEntityConditions;
        }

        public async Task<ICollection<ITriggerSelectQuery<TId>.SelectResult>> ExecuteAsync(
            ITriggerSelectQuery<TId>.IContext context, 
            CancellationToken cancellationToken)
        {
            if (context is not Context typedContext)
            {
                throw new ArgumentException(nameof(context));
            }

            var data = await _dbContext.Set<TriggerDbEntity<TId>>()
                .ApplayQueryCondition(
                    _triggerDbEntityConditions.DbProcessingForSelector.Query, 
                    new ITriggerDbEntityConditions<TId>.DbProcessingForSelectorParameters(
                        typedContext.OffsetId,
                        _dateTimeProvider.UtcNow,
                        typedContext.HandlerKey))
                .Take(typedContext.Options.BatchSize)
                .Select(e => e.Id)
                .ToArrayAsync(cancellationToken);

            var result = new List<ITriggerSelectQuery<TId>.SelectResult>(data.Length);
            foreach (var elem in data)
            {
                result.Add(new ITriggerSelectQuery<TId>.SelectResult(
                    elem,
                    typedContext.HandlerKey));

                if (Comparer<TId>.Default.Compare(elem, typedContext.OffsetId) > 0)
                {
                    typedContext.OffsetId = elem;
                }
            }

            return result;
        }

        public ITriggerSelectQuery<TId>.IContext InitContext(
            ITriggerSelectQuery<TId>.IOptions options, 
            string handlerKey)
        {
            if (options is not OptionsDto typedOptions)
            {
                throw new ArgumentException(nameof(options));
            }

            return new Context()
            {
                Options = typedOptions,
                HandlerKey = handlerKey,
                OffsetId = typedOptions.StartOffset,
            };
        }

        private class Context : ITriggerSelectQuery<TId>.IContext
        {
            public required OptionsDto Options { get; init; }

            public required string HandlerKey { get; init; }

            public required TId OffsetId { get; set; }            
        }

        public class OptionsDto 
            : ITriggerSelectQuery<TId>.IOptions
        {
            public int BatchSize { get; set; }
                = 200;

            public required TId StartOffset { get; set; }
        }
    }
}
