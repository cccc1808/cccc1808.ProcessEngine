using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.QueueModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services
{
    /// <summary>
    /// Воркер чтения очередей.
    /// Queue -> Inbox.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    public class InboxRunner<TId>
        : IInboxRunner
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;

        private WorkersState? State { get; set; }

        public InboxRunner(
            IServiceProvider serviceProvider,
            IQueueProviderFactory _,
            OptionsDto options)
        {
            _serviceProvider = serviceProvider;
            _options = options;
        }

        public async Task StartAsync(
            bool oneExecute)
        {
            await StopAsync();

            var state = new WorkersState()
            {
                ServiceScope = _serviceProvider.CreateAsyncScope(),
                CancellationTokenSource = new CancellationTokenSource(),
                Workers = new List<Task>(_options.Queues.Length),
            };
            State = state;

            foreach (var elem in _options.Queues)
            {
                var task = Task.Run(
                    async () => await Body(
                        state.ServiceScope.ServiceProvider,
                        _options,
                        elem,
                        oneExecute,
                        state.CancellationTokenSource.Token)
                    );
                state.Workers.Add(task);
            }

            if (oneExecute)
            {
                await Task.WhenAll(state.Workers);
            }
        }

        public async Task StopAsync()
        {
            var state = State;
            if (state is null)
            {
                return;
            }

            try
            {
                state.CancellationTokenSource.Cancel();

                await Task.WhenAll(
                    state.Workers);
            }
            finally
            {
                await state.ServiceScope.DisposeAsync();
                State = null;
            }
        }

        public async Task WaitRunningTasksAsync(CancellationToken cancellationToken)
        {
            var state = State;
            await Task.WhenAll(
                state.Workers);
        }

        private static async Task Body(
            IServiceProvider serviceProvider,
            OptionsDto options,
            string queueName,
            bool oneExecute,
            CancellationToken cancelationToken)
        {
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();

            var consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                queueProviderFactory, 
                options.ExceptionDelay, 
                consumer: null, 
                queueName, 
                oneExecute: oneExecute,
                (ex) => {  /*TODO: log*/ },
                cancelationToken);
            while (!cancelationToken.IsCancellationRequested)
            {
                try
                {
                    var batch = await consumer.ConsumeBatchAsync(
                        options.ConsumeBatchLimit,
                        options.ConsumeBatchTimeout,
                        cancelationToken);

                    if (!batch.Any())
                    {
                        continue;
                    }

                    await using (var scope = serviceProvider.CreateAsyncScope())
                    {
                        while (true) 
                        {
                            try
                            {
                                var transactionService = scope.ServiceProvider.GetRequiredService<ITransactionManager>();
                                var consumerService = scope.ServiceProvider.GetRequiredService<IInboxConsumerService>();

                                await using (var transaction = await transactionService.StartTransactionAsync(cancelationToken))
                                {
                                    await consumerService.ProcessBatchAsync(batch, cancelationToken);
                                    await transaction.CommitAsync(cancelationToken);
                                }

                                break;
                            }
                            catch (Exception ex)
                            {
                                // exception в хендлере.
                                if (OperationCancelHelper.IsCancelException(ex, cancelationToken))
                                {
                                    throw;
                                }

                                // TODO: log.

                                if (oneExecute)
                                {
                                    throw;
                                }                                

                                await Task.Delay(options.ExceptionDelay, cancelationToken);
                            }
                        }                        
                    }

                    await consumer.CommitAsync(cancelationToken);

                    if (oneExecute)
                    {
                        break;
                    }
                }
                catch (Exception ex) 
                {
                    // exception при работе с брокером.
                    if (OperationCancelHelper.IsCancelException(ex, cancelationToken))
                    {
                        throw;
                    }

                    // TODO: log.

                    if (oneExecute)
                    {
                        throw;
                    }

                    consumer = await QueuePatternHelper.ConnectOrReconnectConsumerAsync(
                        queueProviderFactory, 
                        options.ExceptionDelay,
                        consumer,
                        queueName,
                        oneExecute: false,
                        (ex) => {  /*TODO: log*/ },
                        cancelationToken);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        private record WorkersState
        {
            public AsyncServiceScope ServiceScope { get; init; }

            public CancellationTokenSource CancellationTokenSource { get; init; } = default!;

            public List<Task> Workers { get; init; } = default!;
        }

        public class OptionsDto
        {
            /// <summary>
            /// Список очередей.
            /// </summary>
            public string[] Queues { get; set; }
                = Array.Empty<string>();

            public int ConsumeBatchLimit { get; set; }
                = 250;

            public TimeSpan ConsumeBatchTimeout { get; set; }
                = TimeSpan.FromSeconds(2);

            public TimeSpan ExceptionDelay { get; set; }
                = TimeSpan.FromSeconds(10);
        }
    }
}
