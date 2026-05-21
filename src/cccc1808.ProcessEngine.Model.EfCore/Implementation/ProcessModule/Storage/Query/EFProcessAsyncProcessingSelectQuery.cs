using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query
{
    public class EFProcessAsyncProcessingSelectQuery<TId, TEntity> 
        : IProcessAsyncProcessingSelectQuery<TId>
        where TEntity : ProcessDbEntity<TId>
    {        
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly ITransactionManager _transactionManager;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessDbEntityConditions<TId, TEntity> _processDbEntityConditions;

        private readonly OptionsDto _options;

        public EFProcessAsyncProcessingSelectQuery(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            ITransactionManager transactionManager,
            ILockQueryHintStore lockQueryHintStore,
            IProcessDbEntityConditions<TId, TEntity> processDbEntityConditions,

            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _transactionManager = transactionManager;
            _lockQueryHintStore = lockQueryHintStore;
            _processDbEntityConditions = processDbEntityConditions;

            _options = options;
        }

        private static Queue<ProcessInstanceInfoDto<TId>> EmptyQueue { get; }
            = new Queue<ProcessInstanceInfoDto<TId>>(1);

        public async IAsyncEnumerable<Queue<ProcessInstanceInfoDto<TId>>> SelectProcessIdsForAsyncProcessingAsync(
            LinkContainer<(object? _, int BatchSize)> context,
            ICollection<ProcessRegistryDto> registrations,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var now = _dateTimeProvider.UtcNow;
                var selectDate = now + _options.SelectoLockDelay;

                ProcessInstanceInfoDto<TId>[] result;
                await using (var transaction = await _transactionManager.StartTransactionAsync(cancellationToken))
                {
                    using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        // TODO: можно сделать без загрузки, но не через LINQ.

                        // В1: нормальный join
                        var registrationQuery = _dbContext.QueryFromCollection(
                            registrations
                            .Select(e => new 
                            {
                                ProcessTypeId = e.ProcessType.ProcessType,
                                ProcessVersion = e.ProcessType.ProcessVersion,
                                Priority = e.Priority,
                            })
                            .ToArray());
                        var query = _dbContext.Set<TEntity>()
                            .Join(
                                registrationQuery,
                                e => new { e.ProcessTypeId, e.ProcessVersion, e.Priority },
                                e => e,
                                (e1, e2) => new { Process = e1, e2 }
                                );
                        query = query.ApplayQueryCondition(
                            _processDbEntityConditions.DbProcessingForSelectorProjection(query),
                            e => e.Process,
                            new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters(now, _dbContext, registrations)
                            );

                        var batch = await query
                            .OrderByDescending(e => e.Process.Priority)
                            .Take(context.Data.BatchSize)
                            .Select(e => new { e.Process.Id, e.Process.ProcessTypeId, e.Process.ProcessVersion, e.Process.Priority })
                            .ToArrayAsync(cancellationToken);

                        // В2 Коррелированный подзапрос
                        //var batch = await _dbContext.Set<TEntity>()
                        //    .ApplayQueryCondition(
                        //        _processDbEntityConditions.DbProcessingForSelector.Query,
                        //        new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters(
                        //            now,
                        //            _dbContext,
                        //            registrations
                        //            ))
                        //    .Take(context.Data.BatchSize)
                        //    .Select(e => new { e.Id, e.ProcessTypeId, e.ProcessVersion, e.Priority })
                        //    .ToArrayAsync(cancellationToken);

                        if (batch.Length == 0)
                        {
                            yield return EmptyQueue;
                        }

                        result = batch
                            .Select(
                                e => new ProcessInstanceInfoDto<TId>(
                                    e.Id,
                                    new ProcessTypeDto(e.ProcessTypeId, e.ProcessVersion),
                                    e.Priority
                                    )
                            )
                            .ToArray();
                    }

                    // Устанавливаем отметку о блокировке выборки.
                    _ = _processDbEntityConditions.DbProcessingForHandler; // для ссылки;
                    await _dbContext.Set<TEntity>()
                        .Where(e => result.Select(e => e.Id).Contains(e.Id))
                        .Where(e => e.Status == ProcessStatusEnum.AsyncExecute)

                        //.ApplayQueryCondition(
                        //    _processDbEntityConditions.DbProcessingForHandler.Query,
                        //    new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForHandlerParameters(
                        //        _dbContext,
                        //        registrations,
                        //        result.Select(e => e.Id).ToArray()
                        //        ))                      
                        .ExecuteUpdateAsync(e => e.SetProperty(e => e.SelectLockTimeout, selectDate), cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                }

                yield return new Queue<ProcessInstanceInfoDto<TId>>(result);
            }
        }

        public async Task UnlockSelectAsync(
            Queue<ProcessInstanceInfoDto<TId>> ids,
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;

            // Снимаем блокировку выборки.
            await _dbContext.Set<TEntity>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.Id.QueryRange, 
                    ids.Select(e => e.Id).ToArray()
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayQueryCondition(_processDbEntityConditions.AsyncExecute.Query)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.SelectLockTimeout, now),
                    cancellationToken);
        }

        public async Task UnlockSelectAsync(
            ICollection<TId> ids, 
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;

            // Снимаем блокировку выборки.
            await _dbContext.Set<TEntity>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.Id.QueryRange,
                    ids
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayQueryCondition(_processDbEntityConditions.AsyncExecute.Query)
                .ExecuteUpdateAsync(
                    e => e.SetProperty(e => e.SelectLockTimeout, now),
                    cancellationToken);
        }

        public record OptionsDto(
            TimeSpan SelectoLockDelay
            );
    }
}
