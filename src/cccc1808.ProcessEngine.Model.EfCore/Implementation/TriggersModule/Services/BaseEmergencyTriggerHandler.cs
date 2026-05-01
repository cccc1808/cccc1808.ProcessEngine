using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
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

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services.Triggers
{
    public class BaseEmergencyTriggerHandler<TId>
        : ITriggerSingleHandler<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Options _options;

        public BaseEmergencyTriggerHandler(
            IServiceProvider serviceProvider,
            Options options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public async ValueTask<ITriggerSingleHandler<TId>.Result> HandleAsync(
            ITriggerComponent<TId> trigger, 
            CancellationToken cancellationToken)
        {
            // todo:
            // Отбираем батч спящих процессов, у которых есть непрочитанные сообщения.
            // При этом их тригер давно не выполнялся.
            // Пропускаем заблокированные.
            // Запускаем процессы.

            var dateTimeProvider = _serviceProvider.GetRequiredService<IDateTimeProvider>();
            var softTimeout = dateTimeProvider.UtcNow.Add(_options.SoftTimeout);
            var stoppedProcessTimeout = dateTimeProvider.UtcNow.Add(-_options.Timeout);

            var haveNotProcessed = true;
            while (haveNotProcessed)
            {
                // soft timeout
                if (dateTimeProvider.UtcNow > softTimeout)
                {
                    break;
                }

                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    // Замечание: транзакция тут особо не нужна.
                    var transactionManager = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                    var dbContext = scope.ServiceProvider.GetRequiredService<IEFDbContext>();
                    var wakeUpService = scope.ServiceProvider.GetRequiredService<IWakeupService<TId>>();

                    var stoppedProcessIds = await Build(
                        scope.ServiceProvider,
                        dbContext,
                        stoppedProcessTimeout,
                        dbContext.Set<ProcessDbEntity<TId>>()
                        )
                        .Take(_options.BatchSize)
                        .Select(e => e.Id)
                        .ToArrayAsync(cancellationToken);

                    haveNotProcessed = _options.BatchSize == stoppedProcessIds.Length;

                    if (stoppedProcessIds.Any())
                    {
                        await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                        {
                            // Можно взять выполниь updatelock skip locked.

                            await wakeUpService.WakeupProcessHandlerAsync(
                                stoppedProcessIds,
                                cancellationToken);

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
                    dateTimeProvider.UtcNow + _options.EmptyTimeout);
            }
            else
            {
                return new ITriggerHandler.Result(
                    true,
                    true,
                    DateTimeOffset.MinValue);
            }
        }


        protected virtual IQueryable<ProcessDbEntity<TId>> Build(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext,
            DateTimeOffset timeout,
            IQueryable<ProcessDbEntity<TId>> source)
        {
            var condition = serviceProvider.GetRequiredService<IProcessDbEntityConditions<TId, ProcessDbEntity<TId>>>();

            return source
                .ApplayQueryCondition(condition.MaybeStoppedByTriggerEventLoosed.QueryRange, timeout);
        }

        public class Options
        {
            public TimeSpan SoftTimeout { get; set; }
                = TimeSpan.FromSeconds(60);

            /// <summary>
            /// Размер батча.
            /// </summary>
            public int BatchSize { get; set; }
                = 250;

            /// <summary>
            /// Задержка, которая идет на проверку.
            /// </summary>
            public TimeSpan Timeout { get; set; }
                = TimeSpan.FromMinutes(20);

            public TimeSpan EmptyTimeout { get; set; }
                = TimeSpan.FromMinutes(20);

            /// <summary>
            /// Для оптимизации sql запроса явн оуказываем перечень типов процессов.
            /// </summary>
            public ProcessRegistryDto[] Types { get; set; } = default!;
        }
    }
}
