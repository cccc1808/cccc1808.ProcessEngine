using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Storage
{
    public class TriggerQueueFacade<TId> 
        : ITriggerQueueFacade<TId>
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ITriggerReservationProvider<TId> _triggerReservationProvider;
        private readonly ITriggerQueueProvider<TId> _triggerQueue;

        private readonly List<ITriggerQueueFacade<TId>.TriggerDto> _triggerToExecuteBuffer;
        private readonly List<ITriggerQueueFacade<TId>.TriggerDto> _triggerContinueRunBuffer;
        private readonly List<TId> _triggerExecutedBuffer;

        private DateTimeOffset? ContinueRunReserveDate { get; set; }

        private bool IsRegistered;

        public TriggerQueueFacade(
            ITransactionManager transactionManager,
            ITriggerReservationProvider<TId> triggerReservationProvider,
            ITriggerQueueProvider<TId> triggerQueue)
        {
            _transactionManager = transactionManager;
            _triggerReservationProvider = triggerReservationProvider;
            _triggerQueue = triggerQueue;

            _triggerToExecuteBuffer = new List<ITriggerQueueFacade<TId>.TriggerDto>(0);
            _triggerContinueRunBuffer = new List<ITriggerQueueFacade<TId>.TriggerDto>(0);
            _triggerExecutedBuffer = new List<TId>(0);
            ContinueRunReserveDate = null;
            IsRegistered = false;
        }

        public void InitExecuteBufferCapacity(int capacity)
        {
            _triggerContinueRunBuffer.Capacity = capacity;
            _triggerExecutedBuffer.Capacity = capacity;
        }

        public void SetContinueRunReserveDate(DateTimeOffset reserveDate)
        {
            ContinueRunReserveDate = reserveDate;
        }

        public void TriggerContinueExecute(ITriggerQueueFacade<TId>.TriggerDto trigger)
        {
            RegisterScopeHandler();
            _triggerContinueRunBuffer.Add(trigger);
        }

        public void TriggerExecuted(TId id)
        {
            RegisterScopeHandler();
            _triggerExecutedBuffer.Add(id);
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
            _triggerToExecuteBuffer.Add(trigger);
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

            transaction.AddAfterCommitHandler(ExecuteAsync, async (t) => { });

            IsRegistered = true;
        }

        private async ValueTask ExecuteAsync(CancellationToken cancellationToken)
        {
            if (_triggerToExecuteBuffer.Any())
            {
                await _triggerReservationProvider.ContinueReserveAsync(
                    _triggerToExecuteBuffer.Select(e => e.GetId()).ToArray(),
                    ContinueRunReserveDate.Value,
                    cancellationToken);
                await _triggerQueue.ProduceTriggersAsync(
                    _triggerToExecuteBuffer
                        .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                            new ITriggerQueueProvider<TId>.MessageDto(e.GetId(), e.HandlerKey),
                            isRangeTrigger: e.IsRangeTrigger)
                        )
                        .ToArray(),
                    cancellationToken);
            }

            if (_triggerContinueRunBuffer.Any())
            {
                await _triggerReservationProvider.ContinueReserveAsync(
                    _triggerContinueRunBuffer.Select(e => e.GetId()).ToArray(),
                    ContinueRunReserveDate.Value, 
                    cancellationToken);
                await _triggerQueue.ProduceTriggersAsync(
                    _triggerContinueRunBuffer
                        .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                            new ITriggerQueueProvider<TId>.MessageDto(e.GetId(), e.HandlerKey),
                            isRangeTrigger: e.IsRangeTrigger)
                        )
                        .ToArray(),
                    cancellationToken);
            }

            if (_triggerExecutedBuffer.Any())
            {
                await _triggerReservationProvider.UnreserveAsync(_triggerExecutedBuffer, cancellationToken);
            }
        }
    }
}
