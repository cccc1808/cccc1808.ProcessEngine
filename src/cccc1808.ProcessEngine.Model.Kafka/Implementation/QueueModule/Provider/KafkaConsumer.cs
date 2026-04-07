using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

using Confluent.Kafka;

namespace cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider
{
    public class KafkaConsumer :
        IQueueConsumer,
        IAsyncDisposable
    {
        private readonly string _topic;
        private readonly IConsumer<string, JsonElement> _consumer;
        private ConsumeResult<string, JsonElement>? _lastMessage;

        public KafkaConsumer(
            string host, 
            string topic,
            string consumerGroup)
        {
            _topic = topic;
            var config = new ConsumerConfig
            {
                BootstrapServers = host,
                GroupId = consumerGroup,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
            };
            var builder = new ConsumerBuilder<string, JsonElement>(config);
            // builder.SetKeyDeserializer(new GuidSerializer());
            builder.SetValueDeserializer(new JsonSerializer());
            _consumer = builder.Build();
            _consumer.Subscribe(topic);
        }        

        public async ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit,
            TimeSpan timeout, 
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            
            var consumeBuffer = new List<MessageDto>(limit);
            ConsumeResult<string, JsonElement>? lastResult = null;
            var stopwatch = Stopwatch.StartNew();

            while (consumeBuffer.Count < limit && stopwatch.Elapsed < timeout)
            {
                var consumeResult = _consumer.Consume(timeout - stopwatch.Elapsed); // this can timeout
                if (consumeResult != null)
                {
                    consumeBuffer.Add(
                        new MessageDto(
                            consumeResult.Message.Key,
                            _topic,
                            consumeResult.Message.Headers
                                .Select(
                                    e => new HeaderDto(e.Key, Encoding.UTF8.GetString(e.GetValueBytes()))
                                    )
                                .ToArray(),
                            consumeResult.Message.Value,
                            consumeResult.Partition.Value
                            ));
                    lastResult = consumeResult;
                    _lastMessage = lastResult;
                }
            }

            stopwatch.Stop();
            return consumeBuffer;
        }

        public async ValueTask ConsumeBatchAsync<TParameter>(
            TParameter parameter,
            TimeSpan packTimeout, 
            int packLimit,
            TimeSpan batchTimeout, 
            Func<TParameter, ICollection<MessageDto>, bool> condition, 
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < batchTimeout)
            {
                var pack = await ConsumeBatchAsync(
                    packLimit, 
                    TimespanHelper.Min(packTimeout, batchTimeout - stopwatch.Elapsed), 
                    cancellationToken);

                var needContinue = condition(parameter, pack);
                if (!needContinue)
                {
                    break;
                }
            }

            stopwatch.Stop();
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            var lastMessage = _lastMessage;
            if (lastMessage == null)
            {
                throw new InvalidOperationException("Не обнаружено считанное сообщение для коммита.");
            }
            _consumer.Commit(lastMessage);
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            try 
            {
                _consumer.Close();
            }
            catch(Exception ex)
            {
                // TODO: log
            }
            finally 
            {
                _consumer.Dispose();
            }            
            return ValueTask.CompletedTask;
        }
    }
}
