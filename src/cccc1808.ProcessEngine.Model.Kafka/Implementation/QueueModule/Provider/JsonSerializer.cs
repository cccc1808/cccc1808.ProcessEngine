using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Confluent.Kafka;

namespace cccc1808.ProcessEngine.Model.Kafka.Implementation.QueueModule.Provider
{
    internal class JsonSerializer :
        ISerializer<JsonElement>,
        IDeserializer<JsonElement>
    {
        public byte[] Serialize(JsonElement data, SerializationContext context)
        {
            return Encoding.UTF8.GetBytes(
                data.GetRawText());
        }

        public JsonElement Deserialize(ReadOnlySpan<byte> data, bool isNull, SerializationContext context)
        {
            var reader = new Utf8JsonReader(data);
            return JsonElement.ParseValue(
                ref reader);
        }
    }
}
