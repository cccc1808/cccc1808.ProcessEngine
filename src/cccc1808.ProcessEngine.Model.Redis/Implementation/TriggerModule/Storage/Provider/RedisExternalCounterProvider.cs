using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Provider
{
    public class RedisExternalCounterProvider : 
        IExternalCounterProvider
    {
        private readonly IRedisConnectionFactory _redisConnectionFactory;

        private readonly OptionsDto _options;

        public RedisExternalCounterProvider(
            IRedisConnectionFactory redisConnectionFactory,

            OptionsDto options)
        {
            _redisConnectionFactory = redisConnectionFactory;

            _options = options;
        }

        #region IExternalCounterProvider

        public async Task CreateCounterAsync(
            string triggerKey, 
            int value, 
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DatabaseId);

            await connection.WaitPiplineWithTimeoutAsync(
                [
                    // Пересоздаем структуры по ключам.
                    db.KeyDeleteAsync(_options.TriggerCounterKeyFactory(triggerKey)),
                    db.KeyDeleteAsync(_options.TriggerMembersKeyFactory(triggerKey)),
                    db.StringIncrementAsync(_options.TriggerCounterKeyFactory(triggerKey), value),
                    db.SetAddAsync(_options.TriggerMembersKeyFactory(triggerKey), "-1"),

                    // Вносим ключи в справочник.
                    db.SetAddAsync(_options.UsingKeysKeyFactory(), _options.TriggerCounterKeyFactory(triggerKey)),
                    db.SetAddAsync(_options.UsingKeysKeyFactory(), _options.TriggerMembersKeyFactory(triggerKey))

                    // db.SetRemoveAsync(_options.TriggerMembersKeyFactory(triggerKey), "-1")
                ],
                cancellationToken
                );
        }

        public async Task RemoveCounterAsync(
            string triggerKey,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DatabaseId);

            await connection.WaitPiplineWithTimeoutAsync(
                [
                    // Удаляем ключи.
                    db.KeyDeleteAsync(_options.TriggerCounterKeyFactory(triggerKey)),
                    db.KeyDeleteAsync(_options.TriggerMembersKeyFactory(triggerKey)),

                    // Удаляем из справочника.
                    db.SetRemoveAsync(_options.UsingKeysKeyFactory(), _options.TriggerCounterKeyFactory(triggerKey)),
                    db.SetRemoveAsync(_options.UsingKeysKeyFactory(), _options.TriggerMembersKeyFactory(triggerKey))
                ],
                cancellationToken
                );
        }

        public async Task<bool> CounterExists(
            string triggerKey,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DatabaseId);

            var key1 = db.KeyExistsAsync(_options.TriggerCounterKeyFactory(triggerKey));
            var key2 = db.KeyExistsAsync(_options.TriggerMembersKeyFactory(triggerKey));

            await connection.WaitPiplineWithTimeoutAsync([key1, key2], cancellationToken);

            return key1.Result && key2.Result;
        }

        public async Task<bool> CompensateCounterAsync(
            string triggerKey,
            string processId)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, CancellationToken.None);
            var db = connection.GetDatabase(_options.DatabaseId);

            //if (!await db.SetContainsAsync(triggerKey, processId))
            //{
            //    return false;
            //}

            var isExecuted = await connection.ExecuteTransactionAsync(
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
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, CancellationToken.None);
            var db = connection.GetDatabase(_options.DatabaseId);

            var result = await connection.ExecuteTransactionAsync(
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
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, CancellationToken.None);
            var db = connection.GetDatabase(_options.DatabaseId);

            await db.SetRemoveAsync(_options.TriggerMembersKeyFactory(triggerKey), processId);
        }

        public async Task<Dictionary<string, (int Counter, ISet<string> Members)>> GetCountersByTriggersAsync(
            ICollection<string> triggersKeys, 
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DatabaseId);

            var result = new Dictionary<string, (int Counter, ISet<string> Members)>(triggersKeys.Count);
            foreach (var elem in triggersKeys)
            {
                var counter = await db.StringGetAsync(_options.TriggerCounterKeyFactory(elem));
                if (!counter.HasValue)
                {
                    continue;
                }

                var members = await db.SetMembersAsync(_options.TriggerMembersKeyFactory(elem));
                result.Add(elem, ((int)counter, members.Select(e => (string)e).ToHashSet()));
            }

            return result;
        }      
        
        public async Task ClearAsync()
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, CancellationToken.None);
            var db = connection.GetDatabase(_options.DatabaseId);

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

        #region types

        public class OptionsDto
        {
            public int MemberSetSizeLimit { get; set; }
                = 100;

            public required string ConnectionName { get; set; }

            public required int DatabaseId { get; set; }

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
