using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB;
using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.ProcessModule.Storage.Query
{
    public class Linq2DbProcessSelectQuery<TId, TEntity> 
        : IProcessAsyncProcessingSelectQuery<TId>
        where TEntity : ProcessDbEntity<TId>
    {        
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly ITransactionManager _transactionManager;
        private readonly IProcessDbEntityConditions<TId, TEntity> _processDbEntityConditions;

        private readonly OptionsDto _options;

        public Linq2DbProcessSelectQuery(
            IDateTimeProvider dateTimeProvider,
            ILinq2DbDataConnection dataConnection,
            ITransactionManager transactionManager,
            IProcessDbEntityConditions<TId, TEntity> processDbEntityConditions,

            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _dataConnection = dataConnection;
            _transactionManager = transactionManager;
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
                    {
                        // TODO: можно сделать без загрузки, но не через LINQ.

                        var query = _dataConnection.Set<TEntity>()
                            .ApplayQueryCondition(
                                _processDbEntityConditions.DbProcessingForSelector.Query,
                                new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters(
                                    now,
                                    registrations)
                                );

                        var batch = await query
                            .QueryHint(PostgresQueryHint.ForNoKeyUpdateSkipLocked)
                            .OrderByDescending(e => e.Priority)
                            .Take(context.Data.BatchSize)
                            .Select(e => new { e.Id, e.ProcessTypeId, e.ProcessVersion, e.Priority })
                            .ToArrayAsync(cancellationToken);

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
                    await _dataConnection.Set<TEntity>()
                        .Where(e => result.Select(e => e.Id).Contains(e.Id))
                        .Where(e => e.Status == ProcessStatusEnum.AsyncExecute)
                        .Set(e => e.SelectLockTimeout, selectDate)
                        .UpdateAsync(cancellationToken);

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
            await _dataConnection.Set<TEntity>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.Id.QueryRange,
                    ids.Select(e => e.Id).ToArray()
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayQueryCondition(_processDbEntityConditions.AsyncExecute.Query)
                .Set(e => e.SelectLockTimeout, now)
                .UpdateAsync(cancellationToken);
        }

        public async Task UnlockSelectAsync(
            ICollection<TId> ids, 
            CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.UtcNow;
            // Снимаем блокировку выборки.
            await _dataConnection.Set<TEntity>()
                .ApplayQueryCondition(
                    _processDbEntityConditions.Id.QueryRange,
                    ids
                    )
                // Для оптимизации - использование фильтрующего индекса.
                .ApplayQueryCondition(_processDbEntityConditions.AsyncExecute.Query)
                .Set(e => e.SelectLockTimeout, now)
                .UpdateAsync(cancellationToken);
        }

        public record OptionsDto(
            TimeSpan SelectoLockDelay
            );
    }
}
