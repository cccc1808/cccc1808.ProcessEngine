using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;
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
        private readonly IQueueProviderFactory _queueProviderFactory;
        private readonly string[] _queues;

        private List<(CancellationTokenSource Token, Task Task)> _consumers;

        public IInboxWorker(
            IServiceProvider serviceProvider, 
            IQueueProviderFactory queueProviderFactory, 
            string[] queues,
            int aggregateCacheSize)
        {
            _serviceProvider = serviceProvider;
            _queueProviderFactory = queueProviderFactory;
            _queues = queues;
            _consumers = new List<(CancellationTokenSource Token, Task Task)>(queues.Length);
        }

        public Task StartAsync()
        {
            _consumers.Clear();
            foreach (var elem in _queues)
            {
                var token = new CancellationTokenSource();
                var task = Task.Run(
                    async () => await Body(elem, token.Token)
                    );
                _consumers.Add((token, task));
            }
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            try 
            {
                foreach (var elem in _consumers)
                {
                    elem.Token.Cancel();
                }

                await Task.WhenAll(
                    _consumers.Select(e => e.Task));
            }
            finally 
            {
                foreach (var elem in _consumers)
                {
                    elem.Token.Dispose();
                }

                _consumers.Clear();
            }
        }


        private async Task Body(
            string queueName,
            CancellationToken cancelationToken)
        {
            var consumer = await _queueProviderFactory.GetConsumerAsync(queueName, cancelationToken);
            while (!cancelationToken.IsCancellationRequested)
            {
                var batch = await consumer.ConsumeBatchAsync(
                    250,
                    TimeSpan.FromSeconds(2),
                    cancelationToken);

                if (batch.Count == 0)
                {
                    continue;
                }

                await using (var scope = _serviceProvider.CreateAsyncScope())
                {
                    var handler = scope.ServiceProvider.GetRequiredService<IEFInboxService>();
                    await handler.ProcessBatchAsync(batch, cancelationToken);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
