using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto
{
    public readonly record struct MessageDto(
        string Key, 
        string Queue,
        HeaderDto[] Headers, 
        JsonElement Body,
        int Partition,
        long Offset)
    {

        public static MessageDto ForSend(
            string Key,
            string Queue,
            HeaderDto[] Headers,
            JsonElement Body,
            int Partition)
            => new MessageDto(
                Key, 
                Queue, 
                Headers,
                Body, 
                Partition, 
                -1);

        public static MessageDto FromConsume(
            string Key,
            string Queue,
            HeaderDto[] Headers,
            JsonElement Body,
            int Partition,
            long Offset) 
            => new MessageDto(
                Key,
                Queue,
                Headers,
                Body,
                Partition,
                Offset);


    }
}
