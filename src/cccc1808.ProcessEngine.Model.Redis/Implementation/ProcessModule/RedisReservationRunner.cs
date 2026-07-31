using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Providers;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule
{
    public class RedisReservationRunner<TId> : IRedisReservationRunner
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly IProcessReservationProvider<TId> _processReservationProvider;
        private readonly IReservationState<TId> _reservationState;

        private readonly RedisReservationOptions _reservationOptions;
        private readonly OptionsDto _options;

        public RedisReservationRunner(
            IDateTimeProvider dateTimeProvider,
            IRedisConnectionFactory redisConnectionFactory,
            IProcessReservationProvider<TId> processReservationProvider,
            IReservationState<TId> reservationState,

            RedisReservationOptions reservationOptions,
            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _redisConnectionFactory = redisConnectionFactory;
            _processReservationProvider = processReservationProvider;
            _reservationState = reservationState;

            _reservationOptions = reservationOptions;
            _options = options;
        }

        public async Task RunSubAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                try
                {
                    await _processReservationProvider.InitAsync(cancellationToken);

                    var connection = await _redisConnectionFactory.GetAsync(_reservationOptions.ConnectionName, cancellationToken);
                    var subscribe = await connection.SubscribeAsync(_reservationOptions.ChannelName, cancellationToken);

                    await foreach (var elem in subscribe.WithCancellation(cancellationToken))
                    {
                        var messageJson = JsonDocument.Parse((string)elem.Message);
                        var message = messageJson.Deserialize<ReservationMessageDto<TId>>();

                        switch (message.IsReserveOrUnreserve)
                        {
                            case true:
                                {
                                    _reservationState.Reserve(message.ProcessId, message.Timeout.Value);
                                    break;
                                }

                            case false:
                                {
                                    _reservationState.Unreserve(message.ProcessId);
                                    break;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                    {
                        throw;
                    }

                    // TODO: log;
                    await Task.Delay(_options.PubSubTaskExceptionDelay, cancellationToken);
                }
            }
        }


        public async Task RunTimeoutAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                _reservationState.ClearTimeout(_dateTimeProvider.UtcNow);
                await Task.Delay(_options.ClearTaskDelay, cancellationToken);
            }
        }

        public class OptionsDto
        {
            public TimeSpan PubSubTaskExceptionDelay { get; set; }

            public TimeSpan ClearTaskDelay { get; set; }
        }
    }
}
