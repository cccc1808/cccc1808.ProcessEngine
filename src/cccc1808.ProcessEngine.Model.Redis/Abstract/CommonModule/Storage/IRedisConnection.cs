using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage
{
    public interface IRedisConnection
    {
        IDatabase GetDatabase(int databaseId);

        ValueTask WaitPiplineWithTimeoutAsync(
            IEnumerable<Task> tasks, 
            CancellationToken cancellationToken);

        ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
            TParam param,
            IDatabase database,
            Func<TParam, ITransaction, TCommands> prepareHandller,
            Func<TParam, TCommands, bool, ValueTask<TResult>> executedHandler);

        ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
            TParam param,
            IDatabase database,
            Func<TParam, ITransaction, TCommands> prepareHandller, 
            Func<TParam, TCommands, bool, TResult> executedHandler);
    }
}
