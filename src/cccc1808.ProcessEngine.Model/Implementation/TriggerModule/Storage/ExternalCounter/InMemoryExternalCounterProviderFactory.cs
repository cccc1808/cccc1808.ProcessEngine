using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage.ExternalCounter
{
    public class InMemoryExternalCounterProviderFactory 
        : IExternalCounterProviderFactory
    {
        private readonly InMemoryExternalCounterProvider _instance;

        public InMemoryExternalCounterProviderFactory()
        {
            _instance = new InMemoryExternalCounterProvider();
        }

        public ValueTask<IExternalCounterProvider> GetProviderAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<IExternalCounterProvider>(_instance);
        }
    }
}
