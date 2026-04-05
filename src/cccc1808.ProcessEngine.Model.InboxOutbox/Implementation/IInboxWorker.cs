using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Services;

using Microsoft.Extensions.DependencyInjection;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation
{
    /// <summary>
    /// Воркер чтения очередей.
    /// Queue -> Inbox.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TDbContext"></typeparam>
    public class IInboxWorker<TId>
        : IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OptionsDto _options;

        private WorkersState? State { get; set; }

        public IInboxWorker(
            IServiceProvider serviceProvider,
            IQueueProviderFactory _,
            OptionsDto options)
        {
            _serviceProvider = serviceProvider;       
            _options = options;
        }

        public async Task StartAsync()
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
                        state.CancellationTokenSource.Token)
                    );
                state.Workers.Add(task);
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

        private static async Task Body(
            IServiceProvider serviceProvider,
            OptionsDto options,
            string queueName,
            CancellationToken cancelationToken)
        {
            var queueProviderFactory = serviceProvider.GetRequiredService<IQueueProviderFactory>();

            var consumer = await queueProviderFactory.GetConsumerAsync(queueName, cancelationToken);
            while (!cancelationToken.IsCancellationRequested)
            {
                var batch = await consumer.ConsumeBatchAsync(
                    options.ConsumeBatchSize,
                    options.ConsumeTimeout,
                    cancelationToken);

                if (batch.Count == 0)
                {
                    continue;
                }

                await using (var scope = serviceProvider.CreateAsyncScope())
                {
                    var handler = scope.ServiceProvider.GetRequiredService<IInboxService>();
                    await handler.ProcessBatchAsync(batch, cancelationToken);
                }

                await consumer.CommitAsync(cancelationToken);
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

            public int ConsumeBatchSize { get; set; } 
                = 250;

            public TimeSpan ConsumeTimeout { get; set; } 
                = TimeSpan.FromSeconds(2);
        }
    }
}
