using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule
{
    public class TriggerOptions<TId>
    {
        public Func<ITriggerEventRaiser<TId>.RaiseContainer, int?> PartitionSelector { get; set; }
            = (_) => (int?)null;

        public TriggerOptions()
        {

        }
    }
}
