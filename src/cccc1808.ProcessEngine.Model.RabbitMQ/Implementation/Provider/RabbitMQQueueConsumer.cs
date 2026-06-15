
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;
using cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider;

using MessagePack;

using RabbitMQ.Client;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Implementation.Provider
{
    public class RabbitMQQueueConsumer : 
        IQueueConsumer,
        IAsyncDisposable
    {
        private readonly string _queueName;
        private readonly IConnection _connection;
        private readonly IChannel _channel;
        private readonly IRabbitMQSerializer _serializer;

        private readonly Queue<ulong> _notCommited 
            = new Queue<ulong>();

        public RabbitMQQueueConsumer(
            string queueName,
            IConnection connection,
            IChannel channel,
            IRabbitMQSerializer serializer)
        {
            _queueName = queueName;
            _connection = connection;
            _channel = channel;
            _serializer = serializer;
        }

        public async ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit, 
            TimeSpan batchTimeout, 
            CancellationToken cancellationToken)
        {
            using var timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutToken.CancelAfter(batchTimeout);

            var consumeBuffer = new List<MessageDto>(limit);
            try
            {
                while (consumeBuffer.Count < limit && !timeoutToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var consumeResult = await _channel.BasicGetAsync(_queueName, autoAck: false, timeoutToken.Token);

                    if (consumeResult != null)
                    {
                        var message = _serializer.Deserialize(consumeResult.Body, _queueName);
                        consumeBuffer.Add(
                            message);

                        _notCommited.Enqueue(consumeResult.DeliveryTag);
                    }
                }

                return consumeBuffer;
            }
            catch (Exception)
            {
                if (timeoutToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return consumeBuffer;
                }

                throw;
            }
        }

        public async ValueTask ConsumeBatchAsync<TParameter>(
            TParameter parameter,
            TimeSpan batchTimeout,
            Func<TParameter, MessageDto, bool> onReceivedHandler, 
            CancellationToken cancellationToken)
        {
            using var timeoutToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutToken.CancelAfter(batchTimeout);

            try
            {
                while (!timeoutToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var consumeResult = await _channel.BasicGetAsync(_queueName, autoAck: false, timeoutToken.Token);
                    if (consumeResult != null)
                    {
                        _notCommited.Enqueue(consumeResult.DeliveryTag);

                        var message = _serializer.Deserialize(consumeResult.Body, _queueName);

                        if (!onReceivedHandler(parameter, message))
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                if (timeoutToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                throw;
            }
        }

        public async ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            while(_notCommited.TryPeek(out var elem))
            {
                await _channel.BasicAckAsync(elem, multiple: false, cancellationToken);
                _notCommited.Dequeue();
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
