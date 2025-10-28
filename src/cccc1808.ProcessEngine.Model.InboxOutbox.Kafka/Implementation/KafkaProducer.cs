using System.Text.Json;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;

using Confluent.Kafka;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Kafka.Implementation
{
    public class KafkaProducer : 
        IQueueProducer, 
        IAsyncDisposable
    {
        private readonly IProducer<string, JsonElement> _producer;

        public KafkaProducer(
            string _host,
            int batchSize)
        {
            var config = new ProducerConfig()
            {
                BootstrapServers = _host,
                BatchNumMessages = batchSize,
                Acks = Acks.All,
                AllowAutoCreateTopics = true,
            };
            var builder = new ProducerBuilder<string, JsonElement>(config);
            // builder.SetKeySerializer(new GuidSerializer());
            builder.SetValueSerializer(new JsonSerializer());
            _producer = builder.Build();
        }        

        public async Task ProduceBatchAsync(
            ICollection<MessageDto> messages,
            CancellationToken cancellationToken)
        {
            await Task.Yield();

            var buffer = new List<Task<DeliveryResult<string, JsonElement>>>(messages.Count);

            foreach (var elem in messages)
            {
                var produceResult = _producer.ProduceAsync(
                    new TopicPartition(
                        elem.Queue,
                        new Partition(elem.Partition)
                        ),
                    new Message<string, JsonElement>()
                    {
                        Key = elem.Key,
                        // Headers = new Headers().Add(new Header()) elem.Headers,
                        Value = elem.Body,
                    },
                    cancellationToken);
                buffer.Add(produceResult);
            }

            try
            {
                await Task.WhenAll(buffer);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public ValueTask DisposeAsync()
        {
            _producer.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
