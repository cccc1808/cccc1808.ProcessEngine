using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using Castle.Components.DictionaryAdapter.Xml;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

using Nito.AsyncEx;

using static cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers.ITriggerHandler;
using static LinqToDB.Reflection.Methods.LinqToDB.Insert;

namespace cccc1808.ProcessEngine.Test2.Infrastructure.Queue
{
    internal class TestInMemoryQueueProviderFactory : IQueueProviderFactory
    {
        private readonly ConcurrentDictionary<string, QueueEntry> _queues 
            = new ConcurrentDictionary<string, QueueEntry>();

        public ValueTask<bool> DisconnectConsumerAsync(string name, CancellationToken cancellationToken)
        {
            var entry = _queues.GetOrAdd(name, _ => new QueueEntry());
            using (var _ = entry.ServiceLock.WriterLock())
            {
                if (entry.ConnectedConsumer == null)
                {
                    return ValueTask.FromResult(false);
                }
                entry.ConnectedConsumer.Disconnect();

                return ValueTask.FromResult(true);
            }
        }

        public ValueTask<IQueueConsumer> GetConsumerAsync(string name, CancellationToken cancellationToken)
        {
            var entry = _queues.GetOrAdd(name, _ => new QueueEntry());
            using (var _ = entry.ServiceLock.ReaderLock())
            {
                var consumer = new TestInMemoryQueueConsumer(entry);
                return ValueTask.FromResult<IQueueConsumer>(consumer);
            }
        }

        public ValueTask<IQueueProducer> GetProducerAsync(string name, CancellationToken cancellationToken)
        {
            var entry = _queues.GetOrAdd(name, _ => new QueueEntry());
            using (var _ = entry.ServiceLock.ReaderLock())
            {
                return ValueTask.FromResult<IQueueProducer>(
                    new TestInMemoryQueueProducer(entry));
            }            
        }

        public class TestInMemoryQueueProducer 
            : IQueueProducer
        {
            private readonly QueueEntry _queue;

            public TestInMemoryQueueProducer(
                QueueEntry queue)
            {
                _queue = queue;
            }

            public Task ProduceBatchAsync(
                ICollection<MessageDto> messages, 
                CancellationToken cancellationToken)
            {
                using (var _ = _queue.ServiceLock.ReaderLock())
                {
                    foreach (var elem in messages)
                    {
                        _queue.Queue.Writer.TryWrite(elem);
                    }
                }

                return Task.CompletedTask;
            }
        }

        public class TestInMemoryQueueConsumer : IQueueConsumer
        {
            private readonly QueueEntry _queue;

            public TestInMemoryQueueConsumer(
                QueueEntry queue)
            {
                if (queue.ConnectedConsumer != null)
                {
                    throw new Exception();
                }

                _queue = queue;
                queue.ConnectedConsumer = this;
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                _queue.NotCommitedMesages.Clear();
                return ValueTask.CompletedTask;
            }

            public async ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
                int limit,
                TimeSpan timeout, 
                CancellationToken cancellationToken)
            {
                return await InnerConsumeBatchAsync(
                    limit, 
                    timeout, 
                    needLock: true,
                    cancellationToken);
            }

            public async ValueTask ConsumeBatchAsync<TParameter>(
                TParameter parameter, 
                TimeSpan packTimeout, 
                int packLimit,
                TimeSpan batchTimeout,
                Func<TParameter, ICollection<MessageDto>, bool> packCondition,
                CancellationToken cancellationToken)
            {
                using (_ = _queue.ServiceLock.ReaderLock())
                {
                    var stopwatch = Stopwatch.StartNew();

                    while (stopwatch.Elapsed < batchTimeout)
                    {
                        var pack = await InnerConsumeBatchAsync(
                            packLimit,
                            TimespanHelper.Min(packTimeout, batchTimeout - stopwatch.Elapsed),
                            false,
                            cancellationToken);

                        var needContinue = packCondition(parameter, pack);
                        if (!needContinue)
                        {
                            break;
                        }
                    }

                    stopwatch.Stop();
                }                
            }

            private async ValueTask<ICollection<MessageDto>> InnerConsumeBatchAsync(
                int limit,
                TimeSpan timeout,
                bool needLock,
                CancellationToken cancellationToken)
            {
                if (needLock)
                {
                    using (_ = _queue.ServiceLock.ReaderLock())
                    {
                        var consumeBuffer = new List<MessageDto>(limit);

                        var stopwatch = Stopwatch.StartNew();

                        while (consumeBuffer.Count < limit && stopwatch.Elapsed < timeout)
                        {
                            await Task.WhenAny(
                                _queue.Queue.Reader.WaitToReadAsync(cancellationToken).AsTask(),
                                Task.Delay(timeout - stopwatch.Elapsed, cancellationToken)
                                );
                            if (_queue.Queue.Reader.TryRead(out var consumeResult))
                            {
                                consumeBuffer.Add(consumeResult);
                                _queue.NotCommitedMesages.Add(consumeResult);
                            }
                        }

                        stopwatch.Stop();

                        return consumeBuffer;
                    }
                }
                else 
                {
                    var consumeBuffer = new List<MessageDto>(limit);

                    var stopwatch = Stopwatch.StartNew();

                    while (consumeBuffer.Count < limit && stopwatch.Elapsed < timeout)
                    {
                        await Task.WhenAny(
                            _queue.Queue.Reader.WaitToReadAsync(cancellationToken).AsTask(),
                            Task.Delay(timeout - stopwatch.Elapsed, cancellationToken)
                            );
                        if (_queue.Queue.Reader.TryRead(out var consumeResult))
                        {
                            consumeBuffer.Add(consumeResult);
                            _queue.NotCommitedMesages.Add(consumeResult);
                        }
                    }

                    stopwatch.Stop();

                    return consumeBuffer;
                }          
            }

            public void Disconnect()
            {
                if (_queue.NotCommitedMesages.Any())
                {
                    while (_queue.Queue.Reader.TryRead(out var readResult))
                    {
                        _queue.NotCommitedMesages.Add(readResult);
                    }

                    foreach (var elem in _queue.NotCommitedMesages)
                    {
                        _queue.Queue.Writer.TryWrite(elem);
                    }

                    _queue.NotCommitedMesages.Clear();                    
                }

                _queue.ConnectedConsumer = null;
            }
        }

        public class QueueEntry 
        {
            public AsyncReaderWriterLock ServiceLock { get; }

            public Channel<MessageDto> Queue { get; }

            public List<MessageDto> NotCommitedMesages { get; }

            public TestInMemoryQueueConsumer? ConnectedConsumer { get; set; }

            public QueueEntry() 
            {
                ServiceLock = new AsyncReaderWriterLock();
                Queue = Channel.CreateUnbounded<MessageDto>(
                    new UnboundedChannelOptions() 
                    {
                        AllowSynchronousContinuations = false, 
                        SingleReader = true, 
                        SingleWriter = false
                    }
                    );
                NotCommitedMesages = new List<MessageDto>();
            }
        }
    }
}
