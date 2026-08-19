using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Reserve
{
    public class RedisTriggerSelectorReserveProvider
        : ITriggerSelectorReserveProvider
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IRedisConnectionFactory _connectionFactory;

        private readonly OptionDto _option;

        public RedisTriggerSelectorReserveProvider(
            IDateTimeProvider dateTimeProvider, 
            IRedisConnectionFactory connectionFactory,
            
            OptionDto option)
        {
            _dateTimeProvider = dateTimeProvider;
            _connectionFactory = connectionFactory;
            _option = option;
        }        

        public async Task<ITriggerSelectorReserveProvider.IReserveScope> TryReserveAsync(
            TriggerTypeUniqueDto triggerTypeUnique,
            DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_option.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_option.DbId);

            var key = new RedisKey(_option.KeyFactory(triggerTypeUnique));
            var insertTask = db.StringSetAsync(
                key,
                RedisValue.EmptyString,
                expiry: date.UtcDateTime,
                when: When.NotExists);
            var getTask = db.StringGetWithExpiryAsync(key);

            await connection.WaitPiplineWithTimeoutAsync(
                [
                    insertTask,
                    getTask
                ],
                cancellationToken);

            var timeout = insertTask.Result
                ? DateTimeOffset.MinValue
                : _dateTimeProvider.UtcNow + (getTask.Result.Expiry ?? TimeSpan.Zero);

            return new ReserveScope(
                db,
                key,
                isSuccess: insertTask.Result,
                timeout);
        }

        public class OptionDto
        {
            public required string ConnectionName { get; set; }
            
            public required int DbId { get; set; }

            public required Func<TriggerTypeUniqueDto, string> KeyFactory { get; set; }
        }

        public class ReserveScope
            : ITriggerSelectorReserveProvider.IReserveScope
        {
            private readonly IDatabase _database;
            private readonly RedisKey _key;

            public bool IsSuccess { get; }

            public DateTimeOffset Timeout { get; }

            private bool NeedUnreserve { get; set; }

            public ReserveScope(
                IDatabase database,
                RedisKey key,
                bool isSuccess,
                DateTimeOffset timeout)
            {
                _database = database;
                _key = key;

                IsSuccess = isSuccess;
                Timeout = timeout;

                NeedUnreserve = isSuccess;
            }

            public async Task UpdateAsync(
                DateTimeOffset date,
                CancellationToken cancellationToken)
            {
                await _database.StringSetAsync(
                    _key, 
                    RedisValue.EmptyString, 
                    expiry: date.UtcDateTime,
                    when: When.Always);
            }

            public void NoUnreserve()
            {
                NeedUnreserve = false;
            }            

            public async ValueTask DisposeAsync()
            {
                if (NeedUnreserve)
                {
                    await _database.KeyDeleteAsync(_key);
                    NeedUnreserve = false;
                }
            }
        }
    }
}
