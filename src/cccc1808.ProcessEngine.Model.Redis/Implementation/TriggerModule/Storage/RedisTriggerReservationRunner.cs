using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule;
using cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage
{
    public class RedisTriggerReservationRunner<TId> 
        : ITriggerRedisReservationRunner
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IRedisConnectionFactory _redisConnectionFactory;
        private readonly ITriggerReservationProvider<TId> _reservationProvider;
        private readonly ITriggerReservationState<TId> _reservationState;

        private readonly RedisTriggerReservationOptions _reservationOptions;
        private readonly OptionsDto _options;

        public RedisTriggerReservationRunner(
            IDateTimeProvider dateTimeProvider,
            IRedisConnectionFactory redisConnectionFactory,
            ITriggerReservationProvider<TId> reservationProvider,
            ITriggerReservationState<TId> reservationState,

            RedisTriggerReservationOptions reservationOptions,
            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _redisConnectionFactory = redisConnectionFactory;
            _reservationProvider = reservationProvider;
            _reservationState = reservationState;

            _reservationOptions = reservationOptions;
            _options = options;
        }

        public async Task RunSubAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _reservationProvider.InitAsync(cancellationToken);

                    var connection = await _redisConnectionFactory.GetAsync(_reservationOptions.ConnectionName, cancellationToken);
                    await using var subscribe = await connection.SubscribeAsync(_reservationOptions.ChannelName, cancellationToken);

                    await foreach (var elem in subscribe.ChannelMessages.WithCancellation(cancellationToken))
                    {
                        var messageJson = JsonDocument.Parse((string)elem.Message);
                        var message = messageJson.Deserialize<ReservationMessageDto<TId>>();

                        if (message.IsReserveOrUnreserve)
                        {
                            _reservationState.Reserve(message.ProcessId, message.Timeout!.Value);
                        }
                        else
                        {
                            _reservationState.Unreserve(message.ProcessId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (OperationCancelHelper.IsCancelException(ex, cancellationToken))
                    {
                        throw;
                    }

                    // TODO: log ex;

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
                = TimeSpan.FromSeconds(2);

            public TimeSpan ClearTaskDelay { get; set; }
                = TimeSpan.FromSeconds(2);
        }
    }
}
