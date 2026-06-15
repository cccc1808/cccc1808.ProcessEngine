using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using MessagePack;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider
{
    [MessagePackObject]
    public struct MessageBinaryDto
    {
        [Key(1)]
        public string Key { get; set; }

        [Key(2)]
        public KeyValuePair<string, string>[] Headers { get; set; }

        [Key(3)]
        public string JsonBody { get; set; }

        public MessageBinaryDto(
            string key, 
            KeyValuePair<string, string>[] headers, 
            string body)
        {
            Key = key;
            Headers = headers;
            JsonBody = body;
        }
    }
}
