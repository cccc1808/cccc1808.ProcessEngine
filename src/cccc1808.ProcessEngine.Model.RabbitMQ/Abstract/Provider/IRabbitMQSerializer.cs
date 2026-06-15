using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider
{
    public interface IRabbitMQSerializer
    {
        byte[] Serialize(
            MessageDto message);

        byte[] SerializeContainer(
            MessageBinaryDto message);

        MessageDto Deserialize(
            ReadOnlyMemory<byte> bytes,
            string queueName);

        MessageBinaryDto DeserializeContainer(
           ReadOnlyMemory<byte> bytes);
    }
}
