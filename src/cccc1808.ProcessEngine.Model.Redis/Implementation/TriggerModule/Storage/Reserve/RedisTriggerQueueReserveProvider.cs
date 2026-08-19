using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Reserve
{
    public class RedisTriggerQueueReserveProvider<TId>
        : ITriggerQueueReserveProvider<TId>
    {
        private readonly IRedisConnectionFactory _connectionFactory;

        private readonly OptionsDto _options;

        public RedisTriggerQueueReserveProvider(
            IRedisConnectionFactory connectionFactory,

            OptionsDto options)
        {
            _connectionFactory = connectionFactory;

            _options = options;
        }

        public async ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> triggerIds,
            DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            // InsertIfNotExists в redis.
            var insertResult = new Dictionary<TId, (string KeyString, Task<bool> Result)>(triggerIds.Count);
            var piplineTasks = new List<Task>(triggerIds.Count + 1);
            foreach (var elem in triggerIds)
            {
                var keyString = _options.KeyToStringHandler(elem);

                var t1 = db.HashSetAsync(_options.HashKey, keyString, date.UtcTicks, when: When.NotExists);

                piplineTasks.Add(t1);

                insertResult.Add(elem, (keyString, t1));
            }
            var t3 = db.HashFieldExpireAsync(
                _options.HashKey,
                insertResult.Values
                    .Select(e => new RedisValue(e.KeyString))
                    .ToArray(),
                date.UtcDateTime,
                when: ExpireWhen.LessThanCurrentExpiry);
            piplineTasks.Add(t3);

            await connection.WaitPiplineWithTimeoutAsync(piplineTasks, cancellationToken);

            var result = new HashSet<TId>(insertResult.Count);
            foreach (var elem in insertResult)
            {
                if (elem.Value.Result.Result)
                {
                    result.Add(elem.Key);
                }
            }

            return result;
        }

        public async ValueTask ContinueReserveAsync(
            ICollection<TId> triggerIds, 
            DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            var keys = triggerIds
                .Select(_options.KeyToStringHandler)
                .ToArray();

            var t1 = db.HashSetAsync(_options.HashKey, keys.Select(e => new HashEntry(e, date.UtcTicks)).ToArray());
            var t2 = db.HashFieldExpireAsync(
                _options.HashKey,
                keys
                    .Select(e => new RedisValue(e))
                    .ToArray(),
                date.UtcDateTime,
                when: ExpireWhen.LessThanCurrentExpiry);

            await connection.WaitPiplineWithTimeoutAsync([t1, t2], cancellationToken);
        }

        public async ValueTask UnreserveAsync(
            ICollection<TId> triggerIds,
            CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DbId);

            // Удаляем из redis.
            await db.HashDeleteAsync(
                _options.HashKey,
                triggerIds
                    .Select(e => new RedisValue(_options.KeyToStringHandler(e)))
                    .ToArray());
        }

        public async ValueTask ClearAsync()
        {
            var connection = await _connectionFactory.GetAsync(_options.ConnectionName, CancellationToken.None);
            var db = connection.GetDatabase(_options.DbId);

            await db.KeyDeleteAsync(_options.HashKey);
        }        

        public class OptionsDto
        {
            public required string ConnectionName { get; set; }

            public required int DbId { get; set; }

            public required string HashKey { get; set; }

            public required Func<TId, string> KeyToStringHandler { get; set; }

            public required Func<string, TId> StringToKeyHandler { get; set; }
        }
    }
}
