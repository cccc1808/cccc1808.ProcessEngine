using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;

using Nito.AsyncEx;

namespace cccc1808.ProcessEngine.Test.Common
{
    public class TestInMemoryQueueProviderFactory : IQueueProviderFactory
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
                TimeSpan batchTimeout, 
                CancellationToken cancellationToken)
            {
                using (_ = _queue.ServiceLock.ReaderLock())
                {
                    var consumeBuffer = new List<MessageDto>(limit);

                    var stopwatch = Stopwatch.StartNew();

                    while (consumeBuffer.Count < limit && stopwatch.Elapsed < batchTimeout)
                    {
                        await Task.WhenAny(
                            _queue.Queue.Reader.WaitToReadAsync(cancellationToken).AsTask(),
                            Task.Delay(batchTimeout - stopwatch.Elapsed, cancellationToken)
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

            public async ValueTask ConsumeBatchAsync<TParameter>(
                TParameter parameter, 
                TimeSpan batchTimeout, 
                Func<TParameter, MessageDto, bool> onReceivedHandler,
                CancellationToken cancellationToken)
            {
                using (_ = _queue.ServiceLock.ReaderLock())
                {
                    var stopwatch = Stopwatch.StartNew();

                    while (stopwatch.Elapsed < batchTimeout)
                    {
                        await Task.WhenAny(
                            _queue.Queue.Reader.WaitToReadAsync(cancellationToken).AsTask(),
                            Task.Delay(batchTimeout - stopwatch.Elapsed, cancellationToken)
                            );

                        if (_queue.Queue.Reader.TryRead(out var consumeResult))
                        {
                            _queue.NotCommitedMesages.Add(consumeResult);
                            if (!onReceivedHandler(parameter, consumeResult))
                            {
                                break;
                            }
                        }
                    }

                    stopwatch.Stop();
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
