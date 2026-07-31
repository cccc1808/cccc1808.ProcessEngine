using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage
{
    public class RedisConnection
        : IRedisConnection,
        IAsyncDisposable
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly SemaphoreSlim _subscribeLock;
        private readonly TimeSpan _pipelineTimeout;

        public RedisConnection(
            ConnectionMultiplexer connectionMultiplexer, 
            TimeSpan pipelineTimeout)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _subscribeLock = new SemaphoreSlim(1, 1);
            _pipelineTimeout = pipelineTimeout;
        }

        public IDatabase GetDatabase(int databaseId)
        {
            return _connectionMultiplexer.GetDatabase(databaseId);            
        }

        public async ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
            TParam param,
            IDatabase database,
            Func<TParam, ITransaction, TCommands> prepareHandller,
            Func<TParam, TCommands, bool, ValueTask<TResult>> executedHandler)
        {
            var transaction = database.CreateTransaction();

            var prepareResult = prepareHandller(param, transaction);

            var isExecuted = await transaction.ExecuteAsync();

            return await executedHandler(param, prepareResult, isExecuted);
        }

        public async ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
            TParam param,
            IDatabase database,
            Func<TParam, ITransaction, TCommands> prepareHandller,
            Func<TParam, TCommands, bool, TResult> executedHandler)
        {
            var transaction = database.CreateTransaction();

            var prepareResult = prepareHandller(param, transaction);

            var isExecuted = await transaction.ExecuteAsync();

            return executedHandler(param, prepareResult, isExecuted);
        }

        public async ValueTask DisposeAsync()
        {
            await _connectionMultiplexer.DisposeAsync();
            _subscribeLock.Dispose();
        }

        public async ValueTask WaitPiplineWithTimeoutAsync(
            IEnumerable<Task> tasks, 
            CancellationToken cancellationToken)
        {
            await Task.WhenAll(tasks);

            //using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            //cancel.CancelAfter(_pipelineTimeout);

            //var completeTask = await Task.WhenAny(
            //    Task.WhenAll(tasks),
            //    Task.Delay(_pipelineTimeout.Add(TimeSpan.FromMinutes(1)), cancel.Token)
            //    );
            //await completeTask;
        }

        public async Task<ChannelMessageQueue> SubscribeAsync(string channel, CancellationToken cancellationToken)
        {
            await _subscribeLock.WaitAsync(cancellationToken);
            try 
            {
                return await _connectionMultiplexer
                    .GetSubscriber()
                    .SubscribeAsync(
                        new RedisChannel(
                            channel,
                            RedisChannel.PatternMode.Literal)
                        );
            }
            finally 
            {
                _subscribeLock.Release();
            }            
        }

        public Task PubAsync(
            string channel, 
            ICollection<JsonElement> messages, 
            CancellationToken cancellationToken)
        {
            var subsriber = _connectionMultiplexer.GetSubscriber();
            var channe = new RedisChannel(
                channel,
                RedisChannel.PatternMode.Literal
                );

            var tasks = new List<Task>(messages.Count);
            foreach (var elem in messages)
            {
                var t = subsriber.PublishAsync(channe, elem.GetRawText());
                tasks.Add(t);
            }

            return Task.WhenAll(tasks);
        }
    }
}
