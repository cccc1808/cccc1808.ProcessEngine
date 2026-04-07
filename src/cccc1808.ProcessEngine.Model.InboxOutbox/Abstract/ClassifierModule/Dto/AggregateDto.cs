using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.ClassifierModule.Dto
{
    public readonly record struct AggregateDto(
        string AggregateType, 
        string AggregateId)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(AggregateType, AggregateId);
        }
    }
}
