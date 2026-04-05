using System;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services
{
    public class BaseEmergencyKeysetTriggerHandler<TId, TProcessData>
        : ITriggerSingleHandler<TId>
        where TProcessData : class, IProcessLinked<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Options _options;

        public BaseEmergencyKeysetTriggerHandler(
            IServiceProvider serviceProvider,
            Options options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public async ValueTask<ITriggerHandler.Result> HandleAsync(
            ITriggerComponent<TId> trigger, CancellationToken cancellationToken)
        {
            // todo:
            // soft timeout
            // Отбираем батч спящих процессов, у которых есть непрочитанные сообщения.
            // При этом их тригер давно не выполнялся.
            // Пропускаем заблокированные.
            // Запускаем процессы.

            var softTimeout = DateTimeOffset.UtcNow.Add(_options.SoftTimeout);
            var stoppedProcessTimeout = DateTimeOffset.UtcNow.Add(-_options.Timeout);

            var offsetId = default(TId);
            var haveNotProcessed = true;
            while (haveNotProcessed)
            {
                // soft timeout
                if (DateTime.UtcNow > softTimeout)
                {
                    break;
                }

                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    // Замечание: транзакция тут особо не нужна.
                    var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                    var wakeUpService = scope.ServiceProvider.GetRequiredService<IWakeupService<TId>>();
                    var condition = scope.ServiceProvider.GetRequiredService<IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>>();
                    var queryHintStore = scope.ServiceProvider.GetRequiredService<ILockQueryHintStore>();

                    var stoppedProcessIds = await Build(
                        scope.ServiceProvider,
                        dbContext,
                        stoppedProcessTimeout
                        )
                        // Keyset paging
                        .Where(e => Comparer<TId>.Default.Compare(e.ProcessId, offsetId) == 1)
                        .OrderBy(e => e.ProcessId)
                        .Select(e => e.ProcessId)
                        .ToArrayAsync(cancellationToken);

                    haveNotProcessed = _options.BatchSize == stoppedProcessIds.Length;

                    if (stoppedProcessIds.Any())
                    {
                        await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                        {                            
                            // Берем блокировку заранее пропуская заблокированные (не обязательно, возмодно убрать).
                            TId[] lockedProcessIds;
                            using (var hint = queryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                            {
                                lockedProcessIds = await dbContext.Set<ProcessDbEntity<TId>>()
                                    .ApplayQueryCondition(condition.Id.QueryRange, stoppedProcessIds)
                                    .Select(e => e.Id)
                                    .ToArrayAsync(cancellationToken);
                            }
                            
                            await wakeUpService.WakeupProcessHandlerAsync(
                                lockedProcessIds,
                                cancellationToken);

                            await dbContext.SaveChangesAsync(cancellationToken);
                            await transaction.CommitAsync(cancellationToken);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (haveNotProcessed)
            {
                return new ITriggerHandler.Result(
                    true,
                    true,
                    DateTimeOffset.UtcNow + _options.EmptyTimeout);
            }
            else
            {
                return new ITriggerHandler.Result(
                    true,
                    true,
                    DateTimeOffset.MinValue);
            }
        }


        protected virtual IQueryable<TProcessData> Build(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext,
            DateTimeOffset timeout)
        {
            return dbContext
                .Set<TProcessData>()
                .Where(
                    e => dbContext.Set<ProcessDbEntity<TId>>()
                        .Where(
                            e2 => 
                                e2.ProcessTypeId.Equals(e.ProcessId)
                                && e2.Status == ProcessStatusEnum.WaitEvent // 1) Процесс в статусе ожидания.
                                && !e2.StoppedByError
                                && e2.RetryCount == null // 2) Процесс не в ошибке.
                                && e2.SelectLockTimeout < timeout) // 3) Процесс давно не брался в обработку.
                        .Any());
        }

        public class Options
        {
            /// <summary>
            /// Размер батча.
            /// </summary>
            public int BatchSize { get; set; }
                = 150;

            /// <summary>
            /// Задержка, которая идет на проверку.
            /// </summary>
            public TimeSpan Timeout { get; set; }
                = TimeSpan.FromSeconds(20);

            public TimeSpan EmptyTimeout { get; set; } 
                = TimeSpan.FromMinutes(10);

            public TimeSpan SoftTimeout { get; set; }
                = TimeSpan.FromSeconds(60);
        }
    }
}
