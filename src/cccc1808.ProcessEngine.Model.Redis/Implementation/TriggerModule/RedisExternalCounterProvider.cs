using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.ExternalCounter;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule
{
    public class RedisExternalCounterProvider : 
        IExternalCounterProvider,
        IAsyncDisposable
    {
        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public RedisExternalCounterProvider(
            ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer;
        }

        public async Task CreateCounterAsync(
            string triggerKey, 
            int value, 
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase();

            await db.StringGetDeleteAsync(triggerKey);
            await db.StringIncrementAsync(triggerKey, value);            
        }

        public async Task RemoveCounterAsync(string triggerKey, CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase();

            await db.StringGetDeleteAsync(triggerKey);

            var members = await GetMembersAsync(db, triggerKey);
            if (members.Any())
            {
                await db.SetRemoveAsync(
                    triggerKey, 
                    members
                        .Select(e => new RedisValue(e))
                        .ToArray()
                        );
            }
        }

        public async Task<bool> CounterExists(
            string triggerKey,
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase();

            var result = await db.StringGetAsync(triggerKey);
            return result.HasValue;
        }

        public async Task<bool> CheckDecrementedAsync(string triggerKey, string processId)
        {
            var db = _connectionMultiplexer.GetDatabase();

            if (!await db.SetRemoveAsync(triggerKey, processId))
            {
                return false;
            }

            await db.StringIncrementAsync(triggerKey, 1);
            return true;
        }        

        public async Task<int> TryDecrementCounterAsync(
            string triggerKey, 
            string processId)
        {
            var db = _connectionMultiplexer.GetDatabase();

            var tran = db.CreateTransaction();
            {
                var isInserted = await db.SetAddAsync(triggerKey, processId);
                if (!isInserted)
                {
                    var counter = await db.StringGetAsync(triggerKey);
                    if (!counter.HasValue)
                    {
                        throw new Exception();
                    }

                    return int.Parse(counter);
                }

                var result = await db.StringIncrementAsync(triggerKey, -1);

                var committed = tran.Execute();
                if (!committed)
                {
                    throw new Exception();
                }

                return (int)result;
            }
        }

        public async Task DecrementCompleteAsync(string triggerKey, string processId)
        {
            var db = _connectionMultiplexer.GetDatabase();

            await db.SetRemoveAsync(triggerKey, processId);
        }

        public async Task<Dictionary<string, (int Counter, ISet<string> Members)>> GetCountersByTriggersAsync(
            ICollection<string> triggersKeys, 
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase();

            var result = new Dictionary<string, (int Counter, ISet<string> Members)>(triggersKeys.Count);
            foreach (var elem in triggersKeys)
            {
                var counter = await db.StringGetAsync(elem);
                if (!counter.HasValue)
                {
                    continue;
                }

                var members = await GetMembersAsync(db, elem);
                result.Add(elem, (int.Parse(counter), members));
            }

            return result;
        }      
        
        private async Task<ISet<string>> GetMembersAsync(IDatabase db, string triggerKey)
        {
            var membersBuffer = new HashSet<string>(0);
            await foreach (var elem2 in db.SetScanAsync(triggerKey))
            {
                membersBuffer.Add(elem2);
            }

            if (membersBuffer.Count == 250)
            {
                throw new Exception();
            }

            return membersBuffer;
        }

        public async ValueTask DisposeAsync()
        {
            await _connectionMultiplexer.DisposeAsync();
        }
    }
}
