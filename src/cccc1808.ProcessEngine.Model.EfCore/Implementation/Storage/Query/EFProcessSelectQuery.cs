using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Query;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.Common.QueryHint;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EntityFrameworkCore.Implementation.Query
{
    public class EFProcessSelectQuery<TId, TEntity> 
        : IProcessSelectQuery<TId>
        where TEntity : ProcessDbEntity<TId>
    {
        private readonly OptionsDto _options;
        private readonly IEFDbContext _dbContext;
        private readonly ITransactionManager _transactionManager;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessDbEntityConditions<TId, TEntity> _processDbEntityConditions;

        public EFProcessSelectQuery(
            OptionsDto options,
            IEFDbContext dbContext,
            ITransactionManager transactionManager,
            ILockQueryHintStore lockQueryHintStore,
            IProcessDbEntityConditions<TId, TEntity> processDbEntityConditions)
        {
            _options = options;
            _dbContext = dbContext;
            _transactionManager = transactionManager;
            _lockQueryHintStore = lockQueryHintStore;
            _processDbEntityConditions = processDbEntityConditions;        }

        private static Queue<ProcessInstanceInfoDto<TId>> EmptyQueue { get; }
            = new Queue<ProcessInstanceInfoDto<TId>>(1);

        public async IAsyncEnumerable<Queue<ProcessInstanceInfoDto<TId>>> SelectAsync(
            IProcessSelectQuery<TId>.ContextDto context,
            ICollection<ProcessRegistryDto> types,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var now = DateTime.UtcNow;
                var selectDate = now + _options.SelectoLockDelay;

                ProcessInstanceInfoDto<TId>[] result;
                await using (var transaction = await _transactionManager.StartTransactionAsync(cancellationToken))
                {
                    using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        // TODO: можно сделать без загрузки, но не через LINQ.
                        var batch = await _dbContext.Set<TEntity>()
                            // Фильтр по типам процессов
                            .ApplayFilterCondition(_processDbEntityConditions.ProcessRegistry.QueryRange, (_dbContext, types))
                            .ApplayFilterCondition(_processDbEntityConditions.AsyncExecute.Query, now)
                            .ApplayFilterCondition(_processDbEntityConditions.SelectLock.Query, now)
                            .OrderByDescending(e => e.Priority)
                            .Take(context.BatchSize)
                            .Select(e => new { e.Id, e.ProcessTypeId, e.ProcessVersion, e.Priority })
                            .ToArrayAsync(cancellationToken);

                        if (batch.Length == 0)
                        {
                            yield return EmptyQueue;
                        }

                        result = batch
                            .Select(
                                e => new ProcessInstanceInfoDto<TId>(
                                    new ProcessIdDto<TId>(e.Id),
                                    new ProcessTypeDto(e.ProcessTypeId, e.ProcessVersion),
                                    e.Priority
                                    )
                            )
                            .ToArray();
                    }

                    // Устанавливаем отметку о блокировке выборки.
                    await _dbContext.Set<TEntity>()
                        .ApplayFilterCondition(
                            _processDbEntityConditions.Id.QueryRange,
                            result.Select(e => e.Id.Id).ToArray()
                            )
                        // Для оптимизации - использование фильтрующего индекса.
                        .ApplayFilterCondition(
                            _processDbEntityConditions.AsyncExecute.Query, 
                            null                        
                            )
                        .ExecuteUpdateAsync(e => e.SetProperty(e => e.SelectLock, selectDate), cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }

                yield return new Queue<ProcessInstanceInfoDto<TId>>(result);
            }
        }

        public async Task UnlockSelectAsync(
            Queue<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            // var now = DateTimeOffset.UtcNow;

            // Снимаем блокировку выборки.
            await _dbContext.Set<TEntity>()
                .ApplayFilterCondition(
                    _processDbEntityConditions.Id.QueryRange, 
                    ids.Select(e => e.Id.Id).ToArray()
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayFilterCondition(_processDbEntityConditions.AsyncExecute.Query, null)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.SelectLock, DateTimeOffset.MinValue.UtcDateTime),
                    cancellationToken);
        }

        public record OptionsDto(
            TimeSpan SelectoLockDelay
            );
    }
}
