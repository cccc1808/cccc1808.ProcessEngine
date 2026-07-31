using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Dto;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Provider
{
    public class RedisProcessReservationProvider<TId>
        : IProcessReservationProvider<TId>
    {
        private readonly IRedisConnectionFactory _connectionFactory;
        private readonly IProcessReservationState<TId> _reservationState;

        private readonly RedisProcessReservationOptions _reservationOptions;
        private readonly OptionsDto _options;

        public RedisProcessReservationProvider(
            IRedisConnectionFactory connectionFactory,
            IProcessReservationState<TId> reservationState,

            RedisProcessReservationOptions reservationOptions,
            OptionsDto options)
        {
            _connectionFactory = connectionFactory;
            _reservationState = reservationState;

            _reservationOptions = reservationOptions;
            _options = options;
        }

        public async ValueTask<ISet<TId>> TryReserveAsync(
            ICollection<TId> processIds,
            DateTimeOffset date,
            CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_reservationOptions.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_reservationOptions.DbId);

            // 1) InsertIfNotExists в redis.
            var insertResult = new Dictionary<TId, (string KeyString, Task<bool> Result)>(processIds.Count);
            var piplineTasks = new List<Task>(processIds.Count + 1);
            foreach (var elem in processIds)
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

            // 2) Публикуем событие для других нод.
            var result = new HashSet<TId>(insertResult.Count);
            var pubMessages = new List<JsonElement>(insertResult.Count);
            foreach (var elem in insertResult)
            {
                if (elem.Value.Result.Result)
                {
                    _reservationState.Reserve(elem.Key, date);
                    result.Add(elem.Key);
                    pubMessages.Add(
                        JsonHelper.ToJsonElement(
                            new ReservationMessageDto<TId>(
                                elem.Key,
                                date,
                                IsReserveOrUnreserve: true)
                            )
                        );
                }
            }

            await connection.PubAsync(
                _reservationOptions.ChannelName,
                pubMessages,
                cancellationToken);

            return result;
        }

        public async ValueTask UnreserveAsync(
            ICollection<TId> processIds, 
            CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_reservationOptions.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_reservationOptions.DbId);

            // 1) Удаляем из redis.
            await db.HashDeleteAsync(
                _options.HashKey, 
                processIds
                    .Select(e => new RedisValue(_options.KeyToStringHandler(e)))
                    .ToArray());

            // 2) Публикуем событие для других нод.
            var pubMessages = new List<JsonElement>(processIds.Count);
            foreach (var elem in processIds) 
            {
                _reservationState.Unreserve(elem);
                pubMessages.Add(
                    JsonHelper.ToJsonElement(
                        new ReservationMessageDto<TId>(
                            elem,
                            null,
                            IsReserveOrUnreserve: false)
                        )
                    );
            }

            await connection.PubAsync(
                _reservationOptions.ChannelName,
                pubMessages,
                cancellationToken);
        }

        public async ValueTask InitAsync(CancellationToken cancellationToken)
        {
            var connection = await _connectionFactory.GetAsync(_reservationOptions.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_reservationOptions.DbId);

            var members = await db.HashGetAllAsync(_options.HashKey);

            foreach (var elem in members)
            {
                var processId = _options.StringToKeyHandler(
                    (string)elem.Name);
                var timeout = new DateTimeOffset((long)elem.Value, TimeSpan.Zero);
                _reservationState.Reserve(processId, timeout);
            }
        }

        public ValueTask<ISet<TId>> GetReservedAsync(CancellationToken cancellationToken)
        {
            // За счет подписки, актуальное состояние поддерживается в буфере.
            var data = _reservationState.GetAll();
            return ValueTask.FromResult(data);
        }

        public async ValueTask ClearAsync()
        {
            var connection = await _connectionFactory.GetAsync(_reservationOptions.ConnectionName, CancellationToken.None);
            var db = connection.GetDatabase(_reservationOptions.DbId);

            await db.KeyDeleteAsync(_options.HashKey);

            _reservationState.Clear();
        }

        public class OptionsDto 
        {
            public string HashKey { get; set; }
                = "process_reservation";

            public required Func<TId, string> KeyToStringHandler { get; set; }

            public required Func<string, TId> StringToKeyHandler { get; set; }
        }
    }
}
