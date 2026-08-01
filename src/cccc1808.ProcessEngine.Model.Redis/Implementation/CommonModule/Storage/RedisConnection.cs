using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage
{
    public class RedisConnection
        : IRedisConnection,
        IAsyncDisposable
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly ConcurrentDictionary<string, LockContainer<SuscribeContainer>> _subscribers;
        private readonly SemaphoreSlim _subscribeLock;
        private readonly TimeSpan _pipelineTimeout;

        public RedisConnection(
            ConnectionMultiplexer connectionMultiplexer, 
            TimeSpan pipelineTimeout)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _subscribers = new ConcurrentDictionary<string, LockContainer<SuscribeContainer>>();
            _subscribeLock = new SemaphoreSlim(1, 1);
            _pipelineTimeout = pipelineTimeout;
        }

        #region IRedisConnection

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

        public async Task<IRedisConnection.ISubscribeContainer> SubscribeAsync(
            string channel, 
            CancellationToken cancellationToken)
        {
            var subscribeContainer = _subscribers.GetOrAdd(channel, static (_) => new LockContainer<SuscribeContainer>());

            await _subscribeLock.WaitAsync(cancellationToken);
            try
            {                
                return await subscribeContainer.Write(
                    (This: this, subscribeContainer, channel),
                    static async (p, e, t) =>
                    {
                        if (e is not null)
                        {
                            throw new InvalidOperationException($"[Bug] Некорректный вызов. Соединение с каналом уже установлено.");
                        }

                        var subscriber = p.This._connectionMultiplexer.GetSubscriber();
                        var subscribe = await subscriber.SubscribeAsync(
                            new RedisChannel(
                                p.channel,
                                RedisChannel.PatternMode.Literal)
                            );
                        return new SuscribeContainer(
                            subscriber,
                            p.This._subscribeLock,
                            p.subscribeContainer,
                            subscribe);
                    },
                    cancellationToken);
            }
            finally
            {
                _subscribeLock.Release();
            }           
        }

        public async Task PubAsync(
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

            await Task.WhenAll(tasks);
        }

        public async Task PubAsync(
            KeyValuePair<string, JsonElement[]>[] messages, 
            CancellationToken cancellation)
        {
            var subsriber = _connectionMultiplexer.GetSubscriber();

            var tasks = new List<Task>(messages[0].Value.Length);
            foreach (var elem in messages)
            {
                var channel = new RedisChannel(
                    elem.Key,
                    RedisChannel.PatternMode.Literal
                    );

                foreach (var elem2 in elem.Value)
                {
                    var t = subsriber.PublishAsync(channel, elem2.GetRawText());
                    tasks.Add(t);
                }
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        public async ValueTask DisposeAsync()
        {
            await _subscribeLock.WaitAsync();
            try 
            {
                await _connectionMultiplexer.GetSubscriber()
                    .UnsubscribeAllAsync();

                foreach (var elem in _subscribers.Values)
                {
                    await elem.Write(
                        1, 
                        static (p, e, t) => 
                        {
                            if (e is not null)
                            {
                                // UnsubscribeAllAsync уже отключил.
                                e.IsDisposed = true;                                
                            }

                            return ValueTask.FromResult<SuscribeContainer>(null);
                        },
                        CancellationToken.None
                        );
                    elem.Dispose();
                }
            }
            finally
            {
                _subscribeLock.Release();
            }
            
            await _connectionMultiplexer.DisposeAsync();
            _subscribeLock.Dispose();
        }

        #region types

        private class SuscribeContainer
            : IRedisConnection.ISubscribeContainer
        {
            private readonly ISubscriber _subscriber;
            private readonly SemaphoreSlim _subscribeLock;
            private readonly LockContainer<SuscribeContainer> _lockContainer;

            public bool IsDisposed { get; set; }

            public ChannelMessageQueue ChannelMessages { get; }

            public SuscribeContainer(
                ISubscriber subscriber, 
                SemaphoreSlim subscribeLock,
                LockContainer<SuscribeContainer> lockContainer,
                ChannelMessageQueue channelMessages)
            {
                _subscriber = subscriber;
                _subscribeLock = subscribeLock;
                _lockContainer = lockContainer;
                ChannelMessages = channelMessages;
            }            

            public async ValueTask DisposeAsync()
            {
                if (IsDisposed)
                {
                    return;
                }

                await _subscribeLock.WaitAsync();
                try 
                {
                    await _lockContainer.Write(
                        this,
                        static async (p, e, t) => 
                        {
                            await p._subscriber.UnsubscribeAsync(p.ChannelMessages.Channel);
                            return null;
                        },
                        CancellationToken.None);                    
                }
                finally
                {
                    _subscribeLock.Release();
                    IsDisposed = true;
                }
            }
        }

        #endregion
    }
}
