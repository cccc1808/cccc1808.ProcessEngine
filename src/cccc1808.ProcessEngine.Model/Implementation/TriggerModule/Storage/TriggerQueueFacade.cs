using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage
{
    public class TriggerQueueFacade<TId> 
        : ITriggerQueueFacade<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITransactionManager _transactionManager;
        private readonly ITriggerReservationProvider<TId> _triggerReservationProvider;
        private readonly ITriggerQueueProvider<TId> _triggerQueue;

        private readonly IsolationContainer<ITriggerQueueFacade<TId>.TriggerDto> _triggerToExecuteBuffer;
        private readonly IsolationContainer<ITriggerQueueFacade<TId>.TriggerDto> _triggerContinueRunBuffer;
        private readonly IsolationContainer<TId> _triggerExecutedBuffer;

        private TimeSpan ReserveTimeout { get; set; }
            = TimeSpan.FromSeconds(30);

        private int BufferCapactity { get; set; }
            = 2;

        private bool IsRegistered;

        public TriggerQueueFacade(
            IDateTimeProvider dateTimeProvider,
            ITransactionManager transactionManager,
            ITriggerReservationProvider<TId> triggerReservationProvider,
            ITriggerQueueProvider<TId> triggerQueue)
        {
            _dateTimeProvider = dateTimeProvider;
            _transactionManager = transactionManager;
            _triggerReservationProvider = triggerReservationProvider;
            _triggerQueue = triggerQueue;

            _triggerToExecuteBuffer = new IsolationContainer<ITriggerQueueFacade<TId>.TriggerDto>(0);
            _triggerContinueRunBuffer = new IsolationContainer<ITriggerQueueFacade<TId>.TriggerDto>(0);
            _triggerExecutedBuffer = new IsolationContainer<TId>(0);
            IsRegistered = false;
        }

        public void InitBufferCapacity(int capacity)
        {
            BufferCapactity = capacity;
        }

        public void SetReserveTimeout(TimeSpan reserveTimeout)
        {
            ReserveTimeout = reserveTimeout;
        }

        public void TriggerContinueExecute(ITriggerQueueFacade<TId>.TriggerDto trigger)
        {
            RegisterScopeHandler();
            _triggerContinueRunBuffer.Add(IsolationContainer.TransactionIsolationIndex, trigger, BufferCapactity);
        }

        public void TriggerExecuted(TId id)
        {
            RegisterScopeHandler();
            _triggerExecutedBuffer.Add(IsolationContainer.TransactionIsolationIndex, id, BufferCapactity);
        }

        public async Task<bool> TriggerFromSelector(
            ICollection<ITriggerQueueFacade<TId>.TriggerDto> triggers,
            DateTimeOffset reserveDate,
            CancellationToken cancellationToken)
        {
            var reserveResult = await _triggerReservationProvider.TryReserveAsync(
                triggers.Select(e => e.GetId()).ToArray(),
                reserveDate,
                cancellationToken);
            return await _triggerQueue.ProduceTriggersAsync(
                triggers
                    .Where(e => reserveResult.Contains(e.GetId()))
                    .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                        new ITriggerQueueProvider<TId>.MessageDto(e.GetId(), e.HandlerKey),
                        isRangeTrigger: e.IsRangeTrigger)
                    )
                    .ToArray(),
                cancellationToken);
        }

        public void TriggerToExecute(ITriggerQueueFacade<TId>.TriggerDto trigger)
        {
            RegisterScopeHandler();
            _triggerToExecuteBuffer.Add(IsolationContainer.TransactionIsolationIndex, trigger, BufferCapactity);
        }

        private void RegisterScopeHandler()
        {
            if (IsRegistered)
            {
                return;
            }

            if (!_transactionManager.TryGetCurrentTransaction(out var transaction))
            {
                throw new Exception("Transaction required.");
            }

            transaction.AddAfterCommitHandler(
                this,
                static async (s, t) => 
                {
                    var typedState = (TriggerQueueFacade<TId>)s;
                    await typedState.ExecuteAsync(t);
                }, 
                static (_, _) => ValueTask.CompletedTask);

            IsRegistered = true;
        }

        private async ValueTask ExecuteAsync(CancellationToken cancellationToken)
        {
            var timeout = _dateTimeProvider.UtcNow + ReserveTimeout;

            if (_triggerToExecuteBuffer.All.Any())
            {
                await _triggerReservationProvider.ContinueReserveAsync(
                    _triggerToExecuteBuffer.All.Select(e => e.GetId()).ToArray(),
                    timeout,
                    cancellationToken);
                await _triggerQueue.ProduceTriggersAsync(
                    _triggerToExecuteBuffer.All
                        .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                            new ITriggerQueueProvider<TId>.MessageDto(e.GetId(), e.HandlerKey),
                            isRangeTrigger: e.IsRangeTrigger)
                        )
                        .ToArray(),
                    cancellationToken);
            }

            if (_triggerContinueRunBuffer.All.Any())
            {
                await _triggerReservationProvider.ContinueReserveAsync(
                    _triggerContinueRunBuffer.All.Select(e => e.GetId()).ToArray(),
                    timeout, 
                    cancellationToken);
                await _triggerQueue.ProduceTriggersAsync(
                    _triggerContinueRunBuffer.All
                        .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                            new ITriggerQueueProvider<TId>.MessageDto(e.GetId(), e.HandlerKey),
                            isRangeTrigger: e.IsRangeTrigger)
                        )
                        .ToArray(),
                    cancellationToken);
            }

            if (_triggerExecutedBuffer.All.Any())
            {
                await _triggerReservationProvider.UnreserveAsync(_triggerExecutedBuffer.All.ToArray(), cancellationToken);
            }
        }
    }
}
