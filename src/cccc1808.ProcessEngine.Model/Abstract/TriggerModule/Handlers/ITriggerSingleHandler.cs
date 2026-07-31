using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers
{
    public interface ITriggerSingleHandler<TId> 
        : ITriggerHandler
    {
        ValueTask<ResultDto> HandleAsync(
            ITriggerComponent<TId> trigger, 
            CancellationToken cancellationToken);
    }
}
