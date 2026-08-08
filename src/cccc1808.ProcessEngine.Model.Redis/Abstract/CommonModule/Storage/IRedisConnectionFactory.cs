using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage
{
    public interface IRedisConnectionFactory
        : IAsyncDisposable
    {
        ValueTask<IRedisConnection> GetAsync(
            string name,
            CancellationToken cancellationToken);

        //// TODO: Disctonnect and failover strategy.
    }
}
