using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider;

using MessagePack;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Implementation.Provider
{
    public class RabbitMQSerializer 
        : IRabbitMQSerializer
    {
        public byte[] SerializeContainer(MessageBinaryDto message)
        {
            return MessagePackSerializer.Serialize(
                message);
        }        

        public byte[] Serialize(MessageDto message)
        {
            var container = new MessageBinaryDto(
                key: message.Key,
                headers: message.Headers
                    .Select(e => new KeyValuePair<string, string>(e.key, e.value))
                    .ToArray(),
                body: message.Body.GetRawText()
                );
            return SerializeContainer(container);
        }

        public MessageBinaryDto DeserializeContainer(ReadOnlyMemory<byte> bytes)
        {
            return MessagePackSerializer.Deserialize<MessageBinaryDto>(
                bytes);
        }

        public MessageDto Deserialize(
            ReadOnlyMemory<byte> bytes,
            string queueName)
        {
            var container = DeserializeContainer(bytes);
            using var bodyJson = JsonDocument.Parse(container.JsonBody);

            return new MessageDto(
                container.Key,
                queueName,
                container.Headers
                    .Select(
                        e => new HeaderDto(e.Key, e.Value)
                        )
                    .ToArray(),
                bodyJson.RootElement.Clone(),
                -1
                );
        }
    }
}
