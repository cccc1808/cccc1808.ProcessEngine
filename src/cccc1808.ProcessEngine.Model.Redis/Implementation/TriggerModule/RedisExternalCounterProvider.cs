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

            // Pipelining
            await Task.WhenAll(
                db.KeyDeleteAsync(GetCounterKey(triggerKey)),
                db.KeyDeleteAsync(GetMemberSetKey(triggerKey)),
                db.StringIncrementAsync(GetCounterKey(triggerKey), value),
                db.SetAddAsync(GetMemberSetKey(triggerKey), "-1"),

                db.SetAddAsync("allKeys", GetCounterKey(triggerKey)),
                db.SetAddAsync("allKeys", GetMemberSetKey(triggerKey))

                // db.SetRemoveAsync(GetMemberSetKey(triggerKey), "-1")
                );
        }

        public async Task RemoveCounterAsync(string triggerKey, CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase();

            // Pipelining
            await Task.WhenAll(
                db.KeyDeleteAsync(GetCounterKey(triggerKey)),
                db.KeyDeleteAsync(GetMemberSetKey(triggerKey)),

                db.SetRemoveAsync("allKeys", GetCounterKey(triggerKey)),
                db.SetRemoveAsync("allKeys", GetMemberSetKey(triggerKey))
                );
        }

        public async Task<bool> CounterExists(
            string triggerKey,
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase();

            var t1 = db.KeyExistsAsync(GetCounterKey(triggerKey));
            var t2 = db.KeyExistsAsync(GetMemberSetKey(triggerKey));

            await Task.WhenAll(t1, t2);

            return t1.Result && t2.Result;
        }

        public async Task<bool> CheckDecrementedAsync(string triggerKey, string processId)
        {
            var db = _connectionMultiplexer.GetDatabase();

            //if (!await db.SetContainsAsync(triggerKey, processId))
            //{
            //    return false;
            //}

            var isExecuted = await ExecuteTransactionAsync(
                (counterKey: GetCounterKey(triggerKey), memberSetKey: GetMemberSetKey(triggerKey), processId),
                db,
                prepareHandller: static (p, t) => 
                {
                    var counterKeyExists = t.AddCondition(
                        Condition.KeyExists(p.counterKey));
                    var memberKeyExists = t.AddCondition(
                        Condition.KeyExists(p.memberSetKey));
                    var memberExsist = t.AddCondition(
                        Condition.SetContains(p.memberSetKey, p.processId));
                    var removeResult = t.SetRemoveAsync(p.memberSetKey, p.processId);
                    var incrementResult = t.StringIncrementAsync(p.counterKey, 1);

                    return (
                        counterKeyExists,
                        memberKeyExists,
                        memberExsist);
                },
                executedHandler: static (p, c, r) => 
                {
                    if (!c.counterKeyExists.WasSatisfied)
                    {
                        throw new Exception();
                    }
                    if (!c.memberKeyExists.WasSatisfied)
                    {
                        throw new Exception();
                    }

                    return !c.memberExsist.WasSatisfied;
                }
                );       

            return isExecuted;

        }        

        public async Task<int> TryDecrementCounterAsync(
            string triggerKey, 
            string processId)
        {
            var db = _connectionMultiplexer.GetDatabase();

            var result = await ExecuteTransactionAsync(
                (counterKey: GetCounterKey(triggerKey), memberSetKey: GetMemberSetKey(triggerKey), processId),
                db,
                prepareHandller: (p, t) => 
                {
                    var counterKeyExists = t.AddCondition(
                        Condition.KeyExists(p.counterKey));
                    var memberKeyExists = t.AddCondition(
                        Condition.KeyExists(p.memberSetKey));
                    var membersCount = t.AddCondition(
                        Condition.SetLengthLessThan(p.memberSetKey, 100));
                    var memberNotExists = t.AddCondition(
                        Condition.SetNotContains(p.memberSetKey, p.processId));                    

                    var insertResult = t.SetAddAsync(p.memberSetKey, p.processId);
                    var incrementResult = t.StringIncrementAsync(p.counterKey, -1);

                    return (
                        counterKeyExists,
                        memberKeyExists,
                        membersCount,
                        memberNotExists,
                        incrementResult);
                },
                executedHandler: (p, c, r) => 
                {
                    if (!c.counterKeyExists.WasSatisfied)
                    {
                        throw new Exception();
                    }
                    if (!c.memberKeyExists.WasSatisfied)
                    {
                        throw new Exception();
                    }
                    if (!c.membersCount.WasSatisfied)
                    {
                        throw new Exception();
                    }

                    return (int)c.incrementResult.Result;
                }
                );

            return result;
        }

        public async Task DecrementCompleteAsync(string triggerKey, string processId)
        {
            var db = _connectionMultiplexer.GetDatabase();

            await db.SetRemoveAsync(GetMemberSetKey(triggerKey), processId);
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
        
        public async Task ClearAsync()
        {
            var db = _connectionMultiplexer.GetDatabase();

            var keys = new List<string>();
            await foreach (var elem in db.SetScanAsync("allKeys"))
            {
                keys.Add(elem);
            }

            await Task.WhenAll(
                keys.Select(
                    e => db.KeyDeleteAsync(e))
                );
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

        private static async ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
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

        private static async ValueTask<TResult> ExecuteTransactionAsync<TParam, TCommands, TResult>(
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

        private static string GetCounterKey(string triggerKey)
        {
            return $"external_counter_{triggerKey}";
        }

        private static string GetMemberSetKey(string triggerKey)
        {
            return $"external_counter_member_{triggerKey}";
        }
    }
}
