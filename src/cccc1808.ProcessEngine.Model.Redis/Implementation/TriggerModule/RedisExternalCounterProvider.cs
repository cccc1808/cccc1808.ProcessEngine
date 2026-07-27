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

        private readonly OptionsDto _options;

        public RedisExternalCounterProvider(
            ConnectionMultiplexer connectionMultiplexer, 
            OptionsDto options)
        {
            _connectionMultiplexer = connectionMultiplexer;
            _options = options;
        }

        #region IExternalCounterProvider

        public async Task CreateCounterAsync(
            string triggerKey, 
            int value, 
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            // Pipelining
            await Task.WhenAll(
                
                // Пересоздаем структуры по ключам.
                db.KeyDeleteAsync(_options.TriggerCounterKeyFactory(triggerKey)),
                db.KeyDeleteAsync(_options.TriggerMembersKeyFactory(triggerKey)),
                db.StringIncrementAsync(_options.TriggerCounterKeyFactory(triggerKey), value),
                db.SetAddAsync(_options.TriggerMembersKeyFactory(triggerKey), "-1"),

                // Вносим ключи в справочник.
                db.SetAddAsync(_options.UsingKeysKeyFactory(), _options.TriggerCounterKeyFactory(triggerKey)),
                db.SetAddAsync(_options.UsingKeysKeyFactory(), _options.TriggerMembersKeyFactory(triggerKey))

                // db.SetRemoveAsync(_options.TriggerMembersKeyFactory(triggerKey), "-1")
                );
        }

        public async Task RemoveCounterAsync(
            string triggerKey,
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            // Pipelining
            await Task.WhenAll(
                // Удаляем ключи.
                db.KeyDeleteAsync(_options.TriggerCounterKeyFactory(triggerKey)),
                db.KeyDeleteAsync(_options.TriggerMembersKeyFactory(triggerKey)),

                // Удаляем из справочника.
                db.SetRemoveAsync(_options.UsingKeysKeyFactory(), _options.TriggerCounterKeyFactory(triggerKey)),
                db.SetRemoveAsync(_options.UsingKeysKeyFactory(), _options.TriggerMembersKeyFactory(triggerKey))
                );
        }

        public async Task<bool> CounterExists(
            string triggerKey,
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            var key1 = db.KeyExistsAsync(_options.TriggerCounterKeyFactory(triggerKey));
            var key2 = db.KeyExistsAsync(_options.TriggerMembersKeyFactory(triggerKey));

            await Task.WhenAll(key1, key2);

            return key1.Result && key2.Result;
        }

        public async Task<bool> CompensateCounterAsync(
            string triggerKey,
            string processId)
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            //if (!await db.SetContainsAsync(triggerKey, processId))
            //{
            //    return false;
            //}

            var isExecuted = await ExecuteTransactionAsync(
                (counterKey: _options.TriggerCounterKeyFactory(triggerKey), memberSetKey: _options.TriggerMembersKeyFactory(triggerKey), processId),
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
                        throw new RedisKeyNotFoundException(p.counterKey);
                    }
                    if (!c.memberKeyExists.WasSatisfied)
                    {
                        throw new RedisKeyNotFoundException(p.memberSetKey);
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
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            var result = await ExecuteTransactionAsync(
                (_options, counterKey: _options.TriggerCounterKeyFactory(triggerKey), memberSetKey: _options.TriggerMembersKeyFactory(triggerKey), processId),
                db,
                prepareHandller: (p, t) => 
                {
                    var counterKeyExists = t.AddCondition(
                        Condition.KeyExists(p.counterKey));
                    var memberKeyExists = t.AddCondition(
                        Condition.KeyExists(p.memberSetKey));
                    var membersCount = t.AddCondition(
                        Condition.SetLengthLessThan(p.memberSetKey, p._options.MemberSetSizeLimit));
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
                        throw new RedisKeyNotFoundException(p.counterKey);
                    }
                    if (!c.memberKeyExists.WasSatisfied)
                    {
                        throw new RedisKeyNotFoundException(p.memberSetKey);
                    }
                    if (!c.membersCount.WasSatisfied)
                    {
                        throw new Exception("Буфер участников заполнен.");
                    }
                    if (c.memberNotExists.WasSatisfied)
                    {
                        return (int?)null;
                    }

                    return (int)c.incrementResult.Result;
                }
                );

            if (!result.HasValue)
            {
                var counterValue = await db.StringGetAsync(_options.TriggerCounterKeyFactory(triggerKey));

                if (!counterValue.HasValue)
                {
                    throw new RedisKeyNotFoundException(_options.TriggerCounterKeyFactory(triggerKey));
                }

                result = (int)counterValue;
            }

            return result.Value;
        }

        public async Task CommitCounterAsync(string triggerKey, string processId)
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            await db.SetRemoveAsync(_options.TriggerMembersKeyFactory(triggerKey), processId);
        }

        public async Task<Dictionary<string, (int Counter, ISet<string> Members)>> GetCountersByTriggersAsync(
            ICollection<string> triggersKeys, 
            CancellationToken cancellationToken)
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            var result = new Dictionary<string, (int Counter, ISet<string> Members)>(triggersKeys.Count);
            foreach (var elem in triggersKeys)
            {
                var counter = await db.StringGetAsync(_options.TriggerCounterKeyFactory(elem));
                if (!counter.HasValue)
                {
                    continue;
                }

                var members = await GetMembersAsync(db, elem);
                result.Add(elem, ((int)counter, members));
            }

            return result;
        }      
        
        public async Task ClearAsync()
        {
            var db = _connectionMultiplexer.GetDatabase(_options.DbId);

            var keys = new List<string>();
            await foreach (var elem in db.SetScanAsync(_options.UsingKeysKeyFactory()))
            {
                keys.Add(elem);
            }

            await Task.WhenAll(
                keys.Select(
                    e => db.KeyDeleteAsync(e))
                .Union(
                    [db.KeyDeleteAsync(_options.UsingKeysKeyFactory())]
                    )
                );
        }

        #endregion

        private async Task<ISet<string>> GetMembersAsync(
            IDatabase db, 
            string triggerKey)
        {
            const int bufferLimit = 250;

            var membersBuffer = new HashSet<string>(0);
            await foreach (var elem2 in db.SetScanAsync(_options.TriggerMembersKeyFactory(triggerKey), pageSize: bufferLimit))
            {
                membersBuffer.Add(elem2);
            }

            if (membersBuffer.Count == bufferLimit)
            {
                throw new Exception("Ошибка переполнения буфера.");
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

        #region types

        public class OptionsDto
        {
            public int MemberSetSizeLimit { get; set; }
                = 100;

            public int DbId { get; set; }
                = -1;

            public Func<string, string> TriggerCounterKeyFactory { get; set; }
                = static (triggerKey) => $"external_counter_{triggerKey}";

            public Func<string, string> TriggerMembersKeyFactory { get; set; }
                = static (triggerKey) => $"external_counter_members_{triggerKey}";

            public Func<string> UsingKeysKeyFactory { get; set; }
                = static () => "external_counter_all_keys";
        }

        public class RedisKeyNotFoundException
            : Exception
        {
            public string Key { get; set; }

            public RedisKeyNotFoundException(string key)
                : base($"Ожидается наличие ключа. {key}")
            {
                Key = key;
            }
        }

        #endregion
    }
}
