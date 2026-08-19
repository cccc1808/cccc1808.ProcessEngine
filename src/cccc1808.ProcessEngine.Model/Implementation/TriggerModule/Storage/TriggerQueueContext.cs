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
    public class TriggerQueueContext<TId> 
        : ITriggerQueueContext<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITransactionManager _transactionManager;
        private readonly ITriggerQueueReserveProvider<TId> _triggerReservationProvider;
        private readonly ITriggerQueueProvider<TId> _triggerQueue;

        private readonly IsolationContainer<ITriggerQueueContext<TId>.TriggerDto> _triggerToExecuteBuffer;
        private readonly IsolationContainer<ITriggerQueueContext<TId>.TriggerDto> _triggerContinueRunBuffer;
        private readonly IsolationContainer<TId> _triggerExecutedBuffer;

        private TimeSpan ReserveTimeout { get; set; }
            = TimeSpan.FromSeconds(30);

        private bool IsRegistered;

        public TriggerQueueContext(
            IDateTimeProvider dateTimeProvider,
            ITransactionManager transactionManager,
            ITriggerQueueReserveProvider<TId> triggerReservationProvider,
            ITriggerQueueProvider<TId> triggerQueue)
        {
            _dateTimeProvider = dateTimeProvider;
            _transactionManager = transactionManager;
            _triggerReservationProvider = triggerReservationProvider;
            _triggerQueue = triggerQueue;

            _triggerToExecuteBuffer = new IsolationContainer<ITriggerQueueContext<TId>.TriggerDto>(0);
            _triggerContinueRunBuffer = new IsolationContainer<ITriggerQueueContext<TId>.TriggerDto>(0);
            _triggerExecutedBuffer = new IsolationContainer<TId>(0);
            IsRegistered = false;
        }

        public void IncreseBufferCapacity(int value)
        {
            _triggerToExecuteBuffer.IncreseCapacity(IsolationContainer.TransactionIsolationIndex, value);
            _triggerContinueRunBuffer.IncreseCapacity(IsolationContainer.TransactionIsolationIndex, value);
            _triggerExecutedBuffer.IncreseCapacity(IsolationContainer.TransactionIsolationIndex, value);
        }

        public void SetReserveTimeout(TimeSpan reserveTimeout)
        {
            ReserveTimeout = reserveTimeout;
        }

        public void TriggerContinueExecute(ITriggerQueueContext<TId>.TriggerDto trigger)
        {
            RegisterScopeHandler();
            _triggerContinueRunBuffer.Add(IsolationContainer.TransactionIsolationIndex, trigger);
        }

        public void TriggerExecuted(TId id)
        {
            RegisterScopeHandler();
            _triggerExecutedBuffer.Add(IsolationContainer.TransactionIsolationIndex, id);
        }

        public async Task<bool> TriggerFromSelector(
            ICollection<ITriggerQueueContext<TId>.TriggerDto> triggers,
            DateTimeOffset reserveDate,
            CancellationToken cancellationToken)
        {
            var reserveResult = await _triggerReservationProvider.TryReserveAsync(
                triggers.Select(e => e.GetId()).ToArray(),
                reserveDate,
                cancellationToken);

            return await ProduceAsync(
                triggers
                    .Where(e => reserveResult.Contains(e.GetId()))
                    .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                        new ITriggerQueueProvider<TId>.MessageDto(e.TypeUnique, e.GetId()),
                        isRangeTrigger: e.IsRangeTrigger)
                    )
                    .ToArray(),
                cancellationToken
                );
        }

        public void TriggerToExecute(ITriggerQueueContext<TId>.TriggerDto trigger)
        {
            RegisterScopeHandler();
            _triggerToExecuteBuffer.Add(IsolationContainer.TransactionIsolationIndex, trigger);
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
                    var typedState = (TriggerQueueContext<TId>)s;
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
                await ProduceAsync(
                    _triggerToExecuteBuffer.All
                        .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                            new ITriggerQueueProvider<TId>.MessageDto(e.TypeUnique, e.GetId()),
                            isRangeTrigger: e.IsRangeTrigger)
                        )
                        .ToArray(),
                    cancellationToken
                    );
            }

            if (_triggerContinueRunBuffer.All.Any())
            {
                await _triggerReservationProvider.ContinueReserveAsync(
                    _triggerContinueRunBuffer.All.Select(e => e.GetId()).ToArray(),
                    timeout, 
                    cancellationToken);
                await ProduceAsync(
                    _triggerContinueRunBuffer.All
                        .Select(e => new ITriggerQueueProvider<TId>.MessageContainer(
                            new ITriggerQueueProvider<TId>.MessageDto(e.TypeUnique, e.GetId()),
                            isRangeTrigger: e.IsRangeTrigger)
                        )
                        .ToArray(),
                    cancellationToken
                    );
            }

            if (_triggerExecutedBuffer.All.Any())
            {
                await _triggerReservationProvider.UnreserveAsync(_triggerExecutedBuffer.All.ToArray(), cancellationToken);
            }
        }

        private async ValueTask<bool> ProduceAsync(
            ICollection<ITriggerQueueProvider<TId>.MessageContainer> messages,
            CancellationToken cancellationToken)
        {
            var notSended = await _triggerQueue.ProduceTriggersAsync(
                messages,
                cancellationToken);

            if (notSended.Any())
            {
                // Не отправлены из-за переполнения очерди - снимаем резервирование.
                await _triggerReservationProvider.UnreserveAsync(
                    notSended,
                    cancellationToken);

                return true;
            }

            return false;
        }        
    }
}
