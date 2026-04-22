using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;

using Confluent.Kafka;

using static cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider.IMessageStore;

namespace cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider
{
    /// <summary>
    /// Провайдер для чтения сообщений из kafka по указанному смещению.
    /// </summary>
    public class KafkaMessageStore
        : IMessageStore
    {
        private readonly ConsumerConfig _consumerConfiguration;

        public KafkaMessageStore(string host) 
        {
            _consumerConfiguration = new ConsumerConfig
            {
                BootstrapServers = host,
                GroupId = "no-use",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false,
            };
        }

        public Task<IDictionary<MessageIdDto, MessageDto>> GetMessagesAsync(
            IMessageStore.MessageIdDto[] keys, 
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<MessageIdDto, MessageDto>(keys.Length);
            var ranges = BuildRanges(keys);

            var builder = new ConsumerBuilder<string, JsonElement>(_consumerConfiguration);
            builder.SetValueDeserializer(new JsonSerializer());

            using (var consumer = builder.Build())
            {
                foreach (var elem in ranges)
                {
                    consumer.Unassign();
                    consumer.Assign(
                        new TopicPartitionOffset(
                            new TopicPartition(
                                elem.Queue,
                                elem.PartitionId),
                            elem.StartOffset
                            )
                        );

                    for (var offset = elem.StartOffset; offset <= elem.EndOffset; offset++) 
                    {
                        // TODO: local timeout.
                        var consumeResult = consumer.Consume(cancellationToken);
                        if (consumeResult is null)
                        {
                            throw new Exception();
                        }
                        if (consumeResult.Offset != offset)
                        {
                            break;
                        }

                        result.Add(
                            new MessageIdDto(
                                elem.Queue,
                                null, 
                                consumeResult.Partition.Value, 
                                consumeResult.Offset.Value
                                ),
                            new MessageDto(
                                consumeResult.Message.Key,
                                elem.Queue,
                                consumeResult.Message.Headers
                                    .Select(
                                        e => new HeaderDto(e.Key, Encoding.UTF8.GetString(e.GetValueBytes()))
                                        )
                                    .ToArray(),
                                consumeResult.Message.Value,
                                consumeResult.Partition.Value
                                ));
                    }
                }

                consumer.Close();
            }

            return Task.FromResult<IDictionary<MessageIdDto, MessageDto>>(result);
        }


        private List<RangeDto> BuildRanges(IMessageStore.MessageIdDto[] keys) 
        {
            var result = new List<RangeDto>(keys.Length);            

            var partitionGroups = keys.GroupBy(e => (e.Queue, e.PartitionId.Value));
            foreach (var elem in partitionGroups)
            {
                RangeDto? currentRange = null;

                foreach (var elem2 in elem.OrderBy(e => e.Offset.Value))
                {
                    if (!currentRange.HasValue)
                    {
                        currentRange = new RangeDto(elem2.Queue, elem2.PartitionId.Value, elem2.Offset.Value, elem2.Offset.Value);
                    }
                    else 
                    {
                        if (elem2.Offset.Value == (currentRange.Value.EndOffset + 1))
                        {
                            // Диапозон продолжается.
                            currentRange = currentRange.Value with { EndOffset = elem2.Offset.Value };
                        }
                        else 
                        {
                            // Диапозон прерывается.
                            result.Add(currentRange.Value);

                            currentRange = new RangeDto(elem2.Queue, elem2.PartitionId.Value, elem2.Offset.Value, elem2.Offset.Value);
                        }
                    }
                }

                result.Add(currentRange!.Value);
            }

            return result;
        }

        private record struct RangeDto(
            string Queue,
            int PartitionId,
            long StartOffset,
            long EndOffset
            );
    }
}
