using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto
{
    public readonly record struct HeaderDto(
        string key,
        string value)
    {
    }
}
