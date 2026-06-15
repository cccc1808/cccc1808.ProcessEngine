using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider;

using MessagePack;

using RabbitMQ.Client;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Implementation.Provider
{
    public class RabbitMQQueueProducer
        : IRabbitMqProducer
    {
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IRabbitMQSerializer _serializer;
        private readonly string _queueName;

        public RabbitMQQueueProducer(
            IConnection connection,
            IChannel channel,
            IRabbitMQSerializer serializer,
            string queueName)
        {
            _connection = connection;
            _channel = channel;
            _serializer = serializer;
            _queueName = queueName;
        }
        
        public async Task ProduceBatchAsync(
            ICollection<MessageDto> messages, 
            CancellationToken cancellationToken)
        {
            foreach (var elem in messages)
            {
                await _channel.BasicPublishAsync(
                    exchange: _queueName,
                    routingKey: _queueName,
                    body: _serializer.Serialize(elem),
                    cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _channel.CloseAsync();
            await _connection.CloseAsync();
            await _channel.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
