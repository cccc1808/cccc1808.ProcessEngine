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
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Query
{
    public class EFIInMemoryQueueProcessRunnerSelectQuery<TId, TEntity> 
        : IInMemoryQueueProcessRunner.ISelectQuery<TId>
        where TEntity : ProcessDbEntity<TId>
    {        
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly ITransactionManager _transactionManager;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessReservationProvider<TId> _processReservationProvider;
        private readonly IProcessDbEntityConditions<TId, TEntity> _processDbEntityConditions;

        private readonly OptionsDto _options;

        public EFIInMemoryQueueProcessRunnerSelectQuery(
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext,
            ITransactionManager transactionManager,
            ILockQueryHintStore lockQueryHintStore,
            IProcessReservationProvider<TId> processReservationProvider,
            IProcessDbEntityConditions<TId, TEntity> processDbEntityConditions,

            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _dbContext = dbContext;
            _transactionManager = transactionManager;
            _lockQueryHintStore = lockQueryHintStore;
            _processReservationProvider = processReservationProvider;
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

                        var reservedProcessIds = await _processReservationProvider.GetReservedAsync(cancellationToken);

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
                            _processDbEntityConditions.DbProcessingForSelectorForProjection1(query),
                            e => e.Process,
                            new IProcessDbEntityConditions<TId, TEntity>.DbProcessingForSelectorParameters(
                                now, 
                                _dbContext, 
                                registrations,
                                reservedProcessIds)
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
                    {
                        var reserved = await _processReservationProvider.TryReserveAsync(
                            result.Select(e => e.Id).ToArray(),
                            selectDate,
                            cancellationToken);

                        result = result.Where(
                            e => reserved.Contains(e.Id))
                            .ToArray();
                    }

                    await transaction.CommitAsync(cancellationToken);
                }

                yield return new Queue<ProcessInstanceInfoDto<TId>>(result);
            }
        }        

        public record OptionsDto(
            TimeSpan SelectoLockDelay
            );
    }
}
