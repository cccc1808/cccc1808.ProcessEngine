using System;
using System.Collections.Generic;
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
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue;

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
        /// Db -> process queue.
        /// </summary>
        public async Task RunProcessDbSelectRunnerAsync(IServiceProvider serviceProvider)
        {
            var processNotifyRunner = serviceProvider.GetRequiredService<IRedisProcessQueueNotificationRunner>();
            var runner = serviceProvider.GetRequiredService<IQueueProcessRunner>();

            var processNotifyRunnerTask = new WaitTaskContainer(
                async (t) => await processNotifyRunner.RunAsync(true, t));

            await WaitRunnerWithTimeoutAsync(
                new WaitTaskContainer(async (t) => await runner.DbSelectExecuteAsync(executeOne: true, t))
                );

            await WaitRunnerWithTimeoutAsync(
                processNotifyRunnerTask);
        }

        /// <summary>
        /// Асинхронная обработка процессов.
        /// </summary>
        public async Task RunProcessRunnerAsync(
            IServiceProvider serviceProvider, 
            bool withProcessNotification, 
            bool isSingle = true)
        {
            var processQueueNotificationRunner = serviceProvider.GetRequiredService<IRedisProcessQueueNotificationRunner>();
            var runner = serviceProvider.GetRequiredService<IQueueProcessRunner>();

            var processNotifyRunnerTask = new WaitTaskContainer((t) => Task.CompletedTask);
            if (withProcessNotification)
            {
                processNotifyRunnerTask = new WaitTaskContainer(
                    async (t) => await processQueueNotificationRunner.RunAsync(true, t));
            }

            if (isSingle)
            {
                await WaitRunnerWithTimeoutAsync(
                    new WaitTaskContainer(
                        async(t) => await runner.RunSingleExecuteAsync(executeOne: true, t))
                    );
            }
            else 
            {
                await WaitRunnerWithTimeoutAsync(
                    new WaitTaskContainer(
                        async(t) => await runner.RunRangeExecuteAsync(executeOne: true, t))
                    );
            }


            if (withProcessNotification)
            {
                await WaitRunnerWithTimeoutAsync(processNotifyRunnerTask);
            }
        }

        #endregion


        #region trigger runner

        /// <summary>
        /// Обработка <see cref="ITriggerEvent"/>.
        /// </summary>
        public async Task RunTriggerConsumerRunnerAsync(IServiceProvider serviceProvider, bool withTriggerNotification)
        {
            var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();
            var triggerQueueNotificationRunner = serviceProvider.GetRequiredService<IRedisTriggerQueueNotificationRunner>();

            var triggerNotifyRunnerTask = new WaitTaskContainer((t) => Task.CompletedTask);
            if (withTriggerNotification)
            {
                // Запускаем Notify, чтобы триггер оповещение поступило в очередь триггеров.
                triggerNotifyRunnerTask = new WaitTaskContainer(
                    async (t) => await triggerQueueNotificationRunner.RunAsync(one: true, t));
            }

            await WaitRunnerWithTimeoutAsync(
                new WaitTaskContainer(async (t) => await triggerRunner.ConsumerWorkAsync(executeOne: true, t))
                );
            (await queueProviderFactory.DisconnectConsumerAsync(triggerOptions.Consumer_TriggerEventQueues.Single().QueueName, default)).ShouldBeTrue();

            if (withTriggerNotification)
            {
                await WaitRunnerWithTimeoutAsync(triggerNotifyRunnerTask);
            }
        }

        /// <summary>
        /// Асинхронная обработка триггеров.
        /// </summary>
        public async Task RunTriggerExecuteRunnerAsync(
            IServiceProvider serviceProvider, 
            bool withTriggerNotification, 
            bool withProcessNotification,
            bool range = true)
        {
            var triggerQueueNotificationRunner = serviceProvider.GetRequiredService<IRedisTriggerQueueNotificationRunner>();
            var processQueueNotificationRunner = serviceProvider.GetRequiredService<IRedisProcessQueueNotificationRunner>();

            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();            

            var triggerNotifyRunnerTask = new WaitTaskContainer((t) => Task.CompletedTask);
            var processNotifyRunnerTask = new WaitTaskContainer((t) => Task.CompletedTask);
            if (withTriggerNotification)
            {
                // Запускаем Notify, чтобы триггер оповещение поступило в очередь триггеров.
                triggerNotifyRunnerTask = new WaitTaskContainer(
                    async (t) => await triggerQueueNotificationRunner.RunAsync(one: true, t)
                    );
            }
            if (withProcessNotification)
            {
                processNotifyRunnerTask = new WaitTaskContainer(
                    async (t) => await processQueueNotificationRunner.RunAsync(true, t)
                    );
            }

            if (range)
            {
                await WaitRunnerWithTimeoutAsync(
                    new WaitTaskContainer(
                        async (t) => await triggerRunner.RangeTriggerProcessingAsync(executeOne: true, t))
                    );
            }
            else
            {
                await WaitRunnerWithTimeoutAsync(
                    new WaitTaskContainer(
                        async (t) => await triggerRunner.SignleTriggerProcessingAsync(executeOne: true, t))
                    );
            }

            if (withTriggerNotification)
            {
                await WaitRunnerWithTimeoutAsync(triggerNotifyRunnerTask);
            }
            if (withProcessNotification)
            {
                await WaitRunnerWithTimeoutAsync(processNotifyRunnerTask);
            }
        }

        public async Task RunTriggerDbSelectRunnerAsync(IServiceProvider serviceProvider, bool withTriggerNotification)
        {
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();
            var triggerQueueNotificationRunner = serviceProvider.GetRequiredService<IRedisTriggerQueueNotificationRunner>();

            var triggerNotifyRunnerTask = new WaitTaskContainer((t) => Task.CompletedTask);
            if (withTriggerNotification)
            {
                // Запускаем Notify, чтобы триггер оповещение поступило в очередь триггеров.
                triggerNotifyRunnerTask = new WaitTaskContainer(
                    async (t) => await triggerQueueNotificationRunner.RunAsync(one: true, t)
                    );
            }

            await WaitRunnerWithTimeoutAsync(
                new WaitTaskContainer(async (t) => await triggerRunner.DbSelectorAsync(executeOne: true, t))
                );

            if (withTriggerNotification)
            {
                await WaitRunnerWithTimeoutAsync(triggerNotifyRunnerTask);
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

        private async Task WaitRunnerWithTimeoutAsync(
            WaitTaskContainer waitTaskContainer)
        {
            var executeTimeout = System.Diagnostics.Debugger.IsAttached
                ? TimeSpan.FromSeconds(120)
                : TimeSpan.FromSeconds(5);

            var waitCancelTimeout = TimeSpan.FromSeconds(10);

            var waitTask = Task.Delay(executeTimeout);

            var resultTask = await Task.WhenAny(
                waitTaskContainer.Task,
                waitTask);

            if (resultTask == waitTask)
            {
                waitTaskContainer.Cancel();

                try
                {
                    var waitTask2 = Task.Delay(waitCancelTimeout);

                    var resultTask2 = await Task.WhenAny(
                        waitTask2,
                        waitTaskContainer.Task
                        );

                    if (waitTask2 == resultTask2)
                    {
                        throw new Exception($"Раннер не реагирует на остановку {nameof(CancellationToken)}.");
                    }

                    await waitTaskContainer.Task;
                }
                catch(Exception ex)
                {
                    throw new Exception(
                        "Раннер завис (не отработал).",
                        ex);
                }

                throw new Exception("Раннер завис (не отработал).");
            }

            await waitTaskContainer.Task;
        }

        private record WaitTaskContainer
        {
            private readonly CancellationTokenSource _cancellationTokenSource;

            public Task Task { get; }

            public WaitTaskContainer(Func<CancellationToken, Task> actionAsync)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                Task = actionAsync(_cancellationTokenSource.Token);
            }

            public void Cancel()
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }
}
