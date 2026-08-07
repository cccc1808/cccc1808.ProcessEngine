using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    /// <summary>
    /// Содержит шаблоны, которые часто используются в тестах, для упрощения кода тестов.
    /// </summary>
    internal class TestService
    {
        #region process runner

        /// <summary>
        /// Асинхронная обработка процессов.
        /// </summary>
        public async Task RunProcessRunnerAsync(IServiceProvider serviceProvider, bool isSingle = true)
        {
            var runner = serviceProvider.GetRequiredService<IQueueProcessRunner>();

            if (isSingle)
            {
                await runner.RunSingleExecuteAsync(executeOne: true, default);
            }
            else 
            {
                await runner.RunRangeExecuteAsync(executeOne: true, default);
            }
        }

        #endregion


        #region trigger runner

        /// <summary>
        /// Обработка <see cref="ITriggerEvent"/>.
        /// </summary>
        public async Task RunTriggerConsumerRunnerAsync(IServiceProvider serviceProvider, bool withNotification)
        {
            var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();
            var queueNotificationRunner = serviceProvider.GetRequiredService<IRedisTriggerQueueNotificationRunner>();

            var notifyRunner = Task.CompletedTask;
            if (withNotification)
            {
                // Запускаем Notify, чтобы триггер оповещение поступило в очередь триггеров.
                notifyRunner = queueNotificationRunner.RunAsync(one: true, default);
            }

            await WaitRunnerWithTimeoutAsync(triggerRunner.ConsumerWorkAsync(executeOne: true, default));
            (await queueProviderFactory.DisconnectConsumerAsync(triggerOptions.Consumer_TriggerEventQueues.Single().QueueName, default)).ShouldBeTrue();

            if (withNotification)
            {
                await WaitRunnerWithTimeoutAsync(notifyRunner);
            }
        }

        /// <summary>
        /// Асинхронная обработка триггеров.
        /// </summary>
        public async Task RunTriggerExecuteRunnerAsync(IServiceProvider serviceProvider, bool withNotification, bool range = true)
        {
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();
            var queueNotificationRunner = serviceProvider.GetRequiredService<IRedisTriggerQueueNotificationRunner>();

            var notifyRunner = Task.CompletedTask;
            if (withNotification)
            {
                // Запускаем Notify, чтобы триггер оповещение поступило в очередь триггеров.
                notifyRunner = queueNotificationRunner.RunAsync(one: true, default);
            }

            if (range)
            {
                await WaitRunnerWithTimeoutAsync(triggerRunner.RangeTriggerProcessingAsync(executeOne: true, default));
            }
            else
            {
                await WaitRunnerWithTimeoutAsync(triggerRunner.SignleTriggerProcessingAsync(executeOne: true, default));
            }

            if (withNotification)
            {
                await WaitRunnerWithTimeoutAsync(notifyRunner);
            }
        }

        public async Task RunTriggerDbSelectRunnerAsync(IServiceProvider serviceProvider, bool withNotification)
        {
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();
            var queueNotificationRunner = serviceProvider.GetRequiredService<IRedisTriggerQueueNotificationRunner>();

            var notifyRunner = Task.CompletedTask;
            if (withNotification)
            {
                // Запускаем Notify, чтобы триггер оповещение поступило в очередь триггеров.
                notifyRunner = queueNotificationRunner.RunAsync(one: true, default);
            }

            await WaitRunnerWithTimeoutAsync(triggerRunner.DbSelectorAsync(executeOne: true, default));

            if (withNotification)
            {
                await WaitRunnerWithTimeoutAsync(notifyRunner);
            }
        }

        #endregion


        #region Trigger events

        public async Task SendTriggerEventAsync(
            IServiceProvider serviceProvider, 
            ITriggerEventRaiser<Guid>.RaiseContainer[] events)
        {
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();
            var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
            var eventRaiser = serviceProvider.GetRequiredService<ITriggerEventRaiser<Guid>>();

            // Посылаем сигнал на дочерний триггер.
            await using (var transaction = await transactionManager.StartTransactionAsync(default))
            {
                await eventRaiser.RaiseAsync(
                    events,
                    default);
                await transaction.CommitAsync(default);
            }
        }

        public async Task SendTriggerEventAsync(
            IServiceProvider serviceProvider,
            ITriggerEvent[] events,
            Guid processId)
        {
            var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();

            await SendTriggerEventAsync(
                serviceProvider,
                events
                    .Select(
                        e => new ITriggerEventRaiser<Guid>.RaiseContainer(
                            triggerOptions.Consumer_TriggerEventQueues.Single().QueueName,
                            processId,
                            e
                            )
                        )
                    .ToArray()
                );
        }

        #endregion


        #region Load

        public async Task<T[]> LoadAsync<T>(IServiceProvider serviceProvider)
            where T : class
        {
            var dbContext = serviceProvider.GetRequiredService<IEFDbContext>();
            return await dbContext.Set<T>().AsNoTracking().ToArrayAsync();
        }

        public async Task<ProcessDbEntity<Guid>[]> LoadProcessAsync(IServiceProvider serviceProvider)
            => await LoadAsync<ProcessDbEntity<Guid>>(serviceProvider);

        public async Task<IProcessContainer<Guid>> LoadProcessContainerAsync(IServiceProvider serviceProvider, Guid id)
        {
            var repository = serviceProvider.GetRequiredService<IProcessRepository<Guid>>();
            var result = await repository.GetWaitingRangeAsync([id], updateLock: false, CancellationToken.None);
            return result.FirstOrDefault();
        }

        public async Task<TriggerDbEntity<Guid>[]> LoadTriggersAsync(IServiceProvider serviceProvider)
            => await LoadAsync<TriggerDbEntity<Guid>>(serviceProvider);

        #endregion


        private async Task WaitRunnerWithTimeoutAsync(Task t)
        {
            var timeout = System.Diagnostics.Debugger.IsAttached
                ? TimeSpan.FromSeconds(120)
                : TimeSpan.FromSeconds(3);

            var waitTask = Task.Delay(timeout);

            var resultTask = await Task.WhenAny(t, waitTask);

            if (resultTask == waitTask)
            {
                throw new Exception("Раннер завис (не отработал).");
            }

            await t;
        }
    }
}
