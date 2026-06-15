using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider;

namespace cccc1808.ProcessEngine.Model.RabbitMQ.Abstract.Provider
{
    public interface IRabbitMqProducer
        : IQueueProducer,
        IAsyncDisposable
    {
    }
}
