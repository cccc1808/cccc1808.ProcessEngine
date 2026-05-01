using System.Text;
using System.Text.Json;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;

using Confluent.Kafka;

namespace cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider
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
                var message = new Message<string, JsonElement>()
                {
                    Key = elem.Key,
                    Value = elem.Body,
                };
                foreach (var elem2 in elem.Headers)
                {
                    message.Headers.Add(elem2.key, Encoding.UTF8.GetBytes(elem2.value));
                }

                var produceResult = _producer.ProduceAsync(
                    new TopicPartition(
                        elem.Queue,
                        new Partition(elem.Partition)
                        ),
                    message,
                    cancellationToken);
                buffer.Add(produceResult);
            }

            // TODO: низкий приоритет (тут можно отследить какие отправились а какие упали с Exception и веделить отдельный хендлер.
            try
            {
                await Task.WhenAll(buffer);
            }
            catch (Exception)
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
