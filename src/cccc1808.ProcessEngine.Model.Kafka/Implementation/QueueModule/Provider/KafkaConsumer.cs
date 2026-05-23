using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;

using Confluent.Kafka;

namespace cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider
{
    public class KafkaConsumer :
        IQueueConsumer,
        IAsyncDisposable
    {
        private readonly string _topic;
        private readonly IConsumer<string, JsonElement> _consumer;
        private Dictionary<int, long> _lastMessagesByPartition
            = new Dictionary<int, long>();

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

        public ValueTask<ICollection<MessageDto>> ConsumeBatchAsync(
            int limit,
            TimeSpan batchTimeout, 
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var consumeBuffer = new List<MessageDto>(limit);                

                while (consumeBuffer.Count < limit && stopwatch.Elapsed < batchTimeout)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var consumeResult = _consumer.Consume(batchTimeout - stopwatch.Elapsed); // this can timeout
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
                        _lastMessagesByPartition[consumeResult.Partition.Value] = consumeResult.Offset.Value;
                    }
                }
                
                return ValueTask.FromResult<ICollection<MessageDto>>(consumeBuffer);
            }
            catch (Exception ex) 
            {
                return ValueTask.FromException<ICollection<MessageDto>>(ex);
            }
            finally 
            {
                stopwatch.Stop();
            }
        }

        public ValueTask ConsumeBatchAsync<TParameter>(
            TParameter parameter, 
            TimeSpan batchTimeout, 
            Func<TParameter, MessageDto, bool> onReceivedHandler, 
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                while (stopwatch.Elapsed < batchTimeout)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var consumeResult = _consumer.Consume(batchTimeout - stopwatch.Elapsed); // this can timeout
                    if (consumeResult != null)
                    {
                        _lastMessagesByPartition[consumeResult.Partition.Value] = consumeResult.Offset.Value;

                        var message = new MessageDto(
                            consumeResult.Message.Key,
                            _topic,
                            consumeResult.Message.Headers
                                .Select(
                                    e => new HeaderDto(e.Key, Encoding.UTF8.GetString(e.GetValueBytes()))
                                    )
                                .ToArray(),
                            consumeResult.Message.Value,
                            consumeResult.Partition.Value
                            );

                        if (!onReceivedHandler(parameter, message))
                        {
                            break;
                        }                        
                    }
                }

                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public ValueTask CommitAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!_lastMessagesByPartition.Any())
                {
                    throw new InvalidOperationException("Не обнаружено считанное сообщение для коммита.");
                }

                _consumer.Commit();

                //_consumer.Commit(
                //    _lastMessagesByPartition
                //        .Select(e => new TopicPartitionOffset(
                //            _topic,
                //            new Partition(e.Key), 
                //            new Offset(e.Value + 1)))
                //        .ToArray());
                _lastMessagesByPartition.Clear();

                return ValueTask.CompletedTask;
            }
            catch (Exception ex) 
            {
                return ValueTask.FromException(ex);
            }
        }

        public ValueTask DisposeAsync()
        {
            try 
            {
                _consumer.Close();
                _lastMessagesByPartition.Clear();
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
