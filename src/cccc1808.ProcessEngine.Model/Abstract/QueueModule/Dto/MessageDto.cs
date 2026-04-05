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
        int Partition)
    {
    }
}
