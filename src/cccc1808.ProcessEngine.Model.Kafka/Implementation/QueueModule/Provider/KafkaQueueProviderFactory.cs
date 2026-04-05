
using System.Collections.Concurrent;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider
{
    public class KafkaQueueProviderFactory
        : IQueueProviderFactory, 
        IAsyncDisposable
    {
        private readonly OptionsDto _options;
        private readonly LockContainer<KafkaProducer> _producer;

        private readonly ConcurrentDictionary<string, LockContainer<string>> _producerTopics;
        private readonly ConcurrentDictionary<string, LockContainer<KafkaConsumer>> _consumers;

        public KafkaQueueProviderFactory(
            OptionsDto options)
        {
            _options = options;
            _producer = new LockContainer<KafkaProducer>();
            _producerTopics = new ConcurrentDictionary<string, LockContainer<string>>();
            _consumers = new ConcurrentDictionary<string, LockContainer<KafkaConsumer>>();            
        }

        public async ValueTask<IQueueConsumer> GetConsumerAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            var container = _consumers.GetOrAdd(name, static (_) => new LockContainer<KafkaConsumer>());

            return await container.DoubleCheckPatternAsync(
                (Options: _options, TopicName: name),
                static (_, consumer) => consumer is not null,
                static (p, t) => ValueTask.FromResult(
                    new KafkaConsumer(
                        p.Options.Host,
                        p.TopicName,
                        p.Options.ConsumerGroupFactory(p.TopicName)
                        )
                    ),
                cancellationToken
                );
        }

        public async ValueTask<IQueueProducer> GetProducerAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            // topic
            {
                var container = _producerTopics.GetOrAdd(name, static (_) => new LockContainer<string>());

                await container.DoubleCheckPatternAsync(
                    (Options: _options, TopicName: name),
                    static (_, producer) => producer is not null,
                    static async (p, t) => 
                    {
                        var admniConfig = new AdminClientConfig()
                        {
                            BootstrapServers = p.Options.Host,
                        };

                        using (var admin = new AdminClientBuilder(admniConfig).Build())
                        {
                            var metadata = admin.GetMetadata(TimeSpan.FromSeconds(10));

                            if (!metadata.Topics.Any(e => e.Topic == p.TopicName))
                            {
                                await admin.CreateTopicsAsync(
                                    [
                                        new TopicSpecification()
                                        {
                                            Name = p.TopicName,
                                            NumPartitions = p.Options.PartitionCountFunc(p.TopicName),
                                        }
                                    ]
                                    );
                            }
                        }

                        return p.TopicName;
                    },
                    cancellationToken
                    );
            }

            {
                return await _producer.DoubleCheckPatternAsync(
                    (Options: _options, TopicName: name),
                    static (_, producer) => producer is not null,
                    static (p, _) => ValueTask.FromResult(new KafkaProducer(p.Options.Host, p.Options.ProducerBatchSize)),
                    cancellationToken
                    );
            }                   
        }

        public async ValueTask DisposeAsync()
        {
            await _producer.Write(
                _producer, 
                static async (p, producer, _) => 
                {
                    if (producer is not null)
                    {
                        try
                        {
                            await producer.DisposeAsync();
                        }
                        catch (Exception ex)
                        {

                        }                        
                    }

                    p.Dispose();

                    return null!;
                }, 
                default);

  
            {
                foreach (var elem in _consumers)
                {
                    await elem.Value.Write(
                        elem.Value,
                        static async (p, consumer, _) =>
                        {
                            if (consumer is not null)
                            {
                                try
                                {
                                    await consumer.DisposeAsync();
                                }
                                catch (Exception ex)
                                {

                                }
                            }

                            p.Dispose();

                            return null!;
                        },
                        default
                        );
                }
            }
        }

        public class OptionsDto 
        {
            public string Host { get; set; }
            public int ProducerBatchSize { get; set; } = 250;
            public Func<string, string> ConsumerGroupFactory { get; set; }
            public Func<string, int> PartitionCountFunc { get; set; }

            public OptionsDto(
                string host,
                int producerBatchSize,
                Func<string, string> consumerGroupFactory,
                Func<string, int> partitionCountFunc
                )
            {
                Host = host;
                ProducerBatchSize = producerBatchSize;
                ConsumerGroupFactory = consumerGroupFactory;
                PartitionCountFunc = partitionCountFunc;                
            }
        }
    }
}
