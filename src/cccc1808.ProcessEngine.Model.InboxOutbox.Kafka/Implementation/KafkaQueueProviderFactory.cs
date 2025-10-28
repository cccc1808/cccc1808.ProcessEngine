
using System.Collections.Concurrent;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Kafka.Implementation
{
    public class KafkaQueueProviderFactory
        : IQueueProviderFactory, 
        IAsyncDisposable
    {
        private readonly SemaphoreSlim _producerLock;
        private readonly string _host;
        private readonly Func<string, string> _consumerGroupFactory;
        private readonly Func<string, int> _partitionCountFunc;
        
        private KafkaProducer? _producer;
        private readonly ConcurrentDictionary<string, Task<KafkaConsumer>> _consumers;

        public KafkaQueueProviderFactory( 
            string host, 
            Func<string, string> consumerGroupFactory, 
            Func<string, int> partitionCountFunc, 
            ConcurrentDictionary<string, Task<KafkaConsumer>> consumers)
        {
            _producerLock = new SemaphoreSlim(1, 1);
            _host = host;
            _consumerGroupFactory = consumerGroupFactory;
            _partitionCountFunc = partitionCountFunc;
            _consumers = consumers;
        }

        public async ValueTask<IQueueConsumer> GetConsumerAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            var result = _consumers.GetOrAdd(
                name,
                (topic) => 
                {
                    return Task.FromResult(
                        new KafkaConsumer(
                            _host, topic, _consumerGroupFactory(topic)
                            )
                        );
                });

            return await result;
        }

        public async ValueTask<IQueueProducer> GetProducerAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            var producer = _producer;
            if (producer != null)
            {
                return producer;
            }            
            
            await _producerLock.WaitAsync();

            producer = _producer;
            if (producer != null)
            {
                return producer;
            }

            {
                var admniConfig = new AdminClientConfig()
                {
                    BootstrapServers = _host,
                };

                using (var admin = new AdminClientBuilder(admniConfig).Build())
                {
                    var metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));

                    if (!metadata.Topics.Any(e => e.Topic == name))
                    {
                        await admin.CreateTopicsAsync(
                            [
                                new TopicSpecification()
                                {
                                    Name = name,
                                    NumPartitions = _partitionCountFunc(name),
                                }
                            ]
                            );
                    }
                }
            }
            {               
                producer = new KafkaProducer(_host, 250);
                _producer = producer;
                return producer;
            }
        }

        public async ValueTask DisposeAsync()
        {
            _producerLock.Dispose();

            if (_producer != null)
            {
                await _producer.DisposeAsync();
            }
            
            foreach (var elem in _consumers)
            {
                try
                {
                    await (await elem.Value).DisposeAsync();
                }
                catch (Exception ex)
                {

                }
            }
        }
    }
}
