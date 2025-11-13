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
using cccc1808.ProcessEngine.Model.Common.Entities.Conditions;
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
        private readonly IId_RangeCondition<TId, TEntity> _id_RangeCondition;
        private readonly Process_ProcessRegistry_RangeCondition<TId, TEntity> _process_ProcessRegistry_RangeCondition;
        private readonly Process_SelectLock_Condition<TId, TEntity> _process_SelectLock_Condition;
        private readonly Process_AsyncExecute_Condition<TId, TEntity> _process_AsyncExecute_Condition;

        public EFProcessSelectQuery(
            OptionsDto options,
            IEFDbContext dbContext, 
            ITransactionManager transactionManager,
            ILockQueryHintStore lockQueryHintStore)
        {
            _options = options;
            _dbContext = dbContext;
            _transactionManager = transactionManager;
            _lockQueryHintStore = lockQueryHintStore;
            _id_RangeCondition = new IId_RangeCondition<TId, TEntity>();
            _process_ProcessRegistry_RangeCondition = new Process_ProcessRegistry_RangeCondition<TId, TEntity>();
            _process_SelectLock_Condition = new Process_SelectLock_Condition<TId, TEntity>();
            _process_AsyncExecute_Condition = new Process_AsyncExecute_Condition<TId, TEntity>();
        }

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
                            .ApplayFilterCondition(_process_ProcessRegistry_RangeCondition, (_dbContext, types))
                            .ApplayFilterCondition(_process_AsyncExecute_Condition, now)
                            .ApplayFilterCondition(_process_SelectLock_Condition, now)
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
                        .ApplayFilterCondition(_id_RangeCondition, result.Select(e => e.Id.Id).ToArray())
                        // Для оптимизации - использование фильтрующего индекса.
                        .ApplayFilterCondition(_process_AsyncExecute_Condition, null)
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
                .ApplayFilterCondition(_id_RangeCondition, ids.Select(e => e.Id.Id).ToArray())
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayFilterCondition(_process_AsyncExecute_Condition, null)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.SelectLock, DateTimeOffset.MinValue.UtcDateTime),
                    cancellationToken);
        }

        public record OptionsDto(
            TimeSpan SelectoLockDelay
            );
    }
}
