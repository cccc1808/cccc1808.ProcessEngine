using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    public interface ITriggerHandlerFactory<TId>
    {
        ITriggerHandler GetHandler(
            IServiceProvider serviceProvider,
            string key);

        bool TryGetHandler(
            IServiceProvider serviceProvider,
            string key,
            out ITriggerHandler handler);
    }
}
