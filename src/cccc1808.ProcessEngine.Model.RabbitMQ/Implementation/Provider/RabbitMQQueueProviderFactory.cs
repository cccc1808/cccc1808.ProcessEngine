using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider;

using RabbitMQ.Client;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Implementation.Provider
{
    public class RabbitMQQueueProviderFactory 
        : IQueueProviderFactory, 
        IAsyncDisposable
    {
        private readonly OptionsDto _options;

        private readonly LockContainer<IRabbitMqProducer> _producer;

        private readonly ConcurrentDictionary<string, LockContainer<RabbitMQQueueConsumer>> _consumers;

        public RabbitMQQueueProviderFactory(
            OptionsDto options)
        {
            _options = options;
            _producer = new LockContainer<IRabbitMqProducer>();
            _consumers = new ConcurrentDictionary<string, LockContainer<RabbitMQQueueConsumer>>();
        }

        public async ValueTask<IQueueConsumer> GetConsumerAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            var container = _consumers.GetOrAdd(name, static (_) => new LockContainer<RabbitMQQueueConsumer>());

            return await container.DoubleCheckPatternAsync(
                (Options: _options, TopicName: name),
                static (_, consumer) => consumer is not null,
                static async (p, t) => 
                {
                    var connection = await OpenConnectionAsync(p.Options, p.TopicName);

                    return new RabbitMQQueueConsumer(
                        p.TopicName,
                        connection.Connection,
                        connection.Channel,
                        new RabbitMQSerializer()
                        );
                },
                cancellationToken
                );
        }

        public async ValueTask<IQueueProducer> GetProducerAsync(string name, CancellationToken cancellationToken)
        {
            return await _producer.DoubleCheckPatternAsync(
                (Options: _options, TopicName: name),
                static (_, producer) => producer is not null,
                static async (p, _) => 
                {
                    var connection = await OpenConnectionAsync(p.Options, p.TopicName);

                    return (IRabbitMqProducer)new ProducerParallelDecorator(
                        new RabbitMQQueueProducer(
                            connection.Connection, 
                            connection.Channel,
                            new RabbitMQSerializer(),
                            p.TopicName)
                        );
                },
                cancellationToken
                );
        }

        public async ValueTask<bool> DisconnectConsumerAsync(
            string name, 
            CancellationToken cancellationToken)
        {
            if (_consumers.TryGetValue(name, out var consumer))
            {
                var isExecuted = false;

                await consumer.Write(
                    (_consumers, consumer, name),
                    async (p, consumer, t) =>
                    {
                        if (consumer == null)
                        {
                            isExecuted = false;
                            return null!;
                        }

                        await consumer.DisposeAsync();
                        //p._consumers.TryRemove(p.name, out _);
                        isExecuted = true;

                        return null!;
                    },
                    cancellationToken);

                return isExecuted;
            }

            return false;
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
                            // TODO: log.
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
                                    // TODO: log.
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

        private static async Task<(IConnection Connection, IChannel Channel)> OpenConnectionAsync(
            OptionsDto options,
            string queueName)
        {
            ConnectionFactory factory = new ConnectionFactory();
            factory.Uri = new Uri(options.Host);

            var conn = await factory.CreateConnectionAsync();
            var channel = await conn.CreateChannelAsync();

            await channel.ExchangeDeclareAsync(exchange: queueName, ExchangeType.Direct);
            await channel.QueueDeclareAsync(
                queue: queueName,
                durable: false,
                exclusive: false, 
                autoDelete: false,
                null);
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: queueName, 
                routingKey: queueName, null);

            return (conn, channel);
        }

        public class OptionsDto
        {
            public string Host { get; set; }

            public OptionsDto(
                string host
                )
            {
                Host = host;
            }
        }
    }
}
