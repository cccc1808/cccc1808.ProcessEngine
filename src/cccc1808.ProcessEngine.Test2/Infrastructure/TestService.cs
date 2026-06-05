using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

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
        public async Task RunProcessRunnerAsync(IServiceProvider serviceProvider)
        {
            var runner = serviceProvider.GetRequiredService<IProcessRunner>();

            await runner.RunAsync(oneCycle: true, default);
            await runner.WaitRunningTasksAsync(default);
        }

        #endregion


        #region trigger runner

        /// <summary>
        /// Обработка <see cref="ITriggerEvent"/>.
        /// </summary>
        public async Task RunTriggerConsumerRunnerAsync(IServiceProvider serviceProvider)
        {
            var triggerOptions = serviceProvider.GetRequiredService<TriggerRunner<Guid>.OptionsDto>();
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();

            await triggerRunner.ConsumerWorkAsync(executeOne: true, default);
            (await queueProviderFactory.DisconnectConsumerAsync(triggerOptions.TriggerEventQueues.Single().QueueName, default)).ShouldBeTrue();
        }

        /// <summary>
        /// Асинхронная обработка триггеров.
        /// </summary>
        public async Task RunTriggerDbRunnerAsync(IServiceProvider serviceProvider)
        {
            var triggerRunner = serviceProvider.GetRequiredService<ITriggerRunner>();

            await triggerRunner.DbWorkAsync(executeOne: true, default);
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
                            triggerOptions.TriggerEventQueues.Single().QueueName,
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

        public async Task<TriggerDbEntity<Guid>[]> LoadTriggersAsync(IServiceProvider serviceProvider)
            => await LoadAsync<TriggerDbEntity<Guid>>(serviceProvider);

        #endregion
    }
}
