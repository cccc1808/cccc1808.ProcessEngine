using System.Diagnostics;
using System.Text;
using System.Text.Json;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;

using Confluent.Kafka;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Kafka.Implementation
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

            var stopwatch = new Stopwatch();
            var consumeBuffer = new List<MessageDto>(limit);
            ConsumeResult<string, JsonElement>? lastResult = null;

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
            return consumeBuffer;
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            var lastMessage = _lastMessage;
            if (lastMessage == null)
            {
                throw new InvalidOperationException();
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

            }
            finally 
            {
                _consumer.Dispose();
            }            
            return ValueTask.CompletedTask;
        }
    }
}
