using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal class StubRootTriggerQuery<TId>
        : IRootTriggerQuery<TId>
    {
        public ValueTask<string?> GetRootTriggerKeyAsync(
            ITriggerComponent<TId> triggerComponent,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException("Пока не нужно для тестов.");
        }
    }
}
