using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.CommonModule.Storage
{
    public class RedisConnection
        : IRedisConnection,
        IAsyncDisposable
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;
        private readonly TimeSpan _pipelineTimeout;

        public RedisConnection(
            ConnectionMultiplexer connectionMultiplexer, 
            TimeSpan pipelineTimeout)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _pipelineTimeout = pipelineTimeout;
        }

        public IDatabase GetDatabase(int databaseId)
        {
            return _connectionMultiplexer.GetDatabase(databaseId);
        }

        public async ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
            TParam param,
            IDatabase database,
            Func<TParam, ITransaction, TCommands> prepareHandller,
            Func<TParam, TCommands, bool, ValueTask<TResult>> executedHandler)
        {
            var transaction = database.CreateTransaction();

            var prepareResult = prepareHandller(param, transaction);

            var isExecuted = await transaction.ExecuteAsync();

            return await executedHandler(param, prepareResult, isExecuted);
        }

        public async ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
            TParam param,
            IDatabase database,
            Func<TParam, ITransaction, TCommands> prepareHandller,
            Func<TParam, TCommands, bool, TResult> executedHandler)
        {
            var transaction = database.CreateTransaction();

            var prepareResult = prepareHandller(param, transaction);

            var isExecuted = await transaction.ExecuteAsync();

            return executedHandler(param, prepareResult, isExecuted);
        }

        public async ValueTask DisposeAsync()
        {
            await _connectionMultiplexer.DisposeAsync();
        }

        public async ValueTask WaitPiplineWithTimeoutAsync(
            IEnumerable<Task> tasks, 
            CancellationToken cancellationToken)
        {
            await Task.WhenAll(tasks);

            //using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            //cancel.CancelAfter(_pipelineTimeout);

            //var completeTask = await Task.WhenAny(
            //    Task.WhenAll(tasks),
            //    Task.Delay(_pipelineTimeout.Add(TimeSpan.FromMinutes(1)), cancel.Token)
            //    );
            //await completeTask;
        }        
    }
}
