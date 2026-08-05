using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.Provider
{
    public class ProcessQueueFacade<TId>
        : IProcessQueueFacade<TId>
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITransactionManager _transactionManager;
        private readonly IIsolationService _isolationService;
        private readonly IProcessQueueProvider<TId> _processQueueProvider;
        private readonly IProcessReservationProvider<TId> _processReservationProvider;

        private HashSet<int> _registeredScopes;

        private IsolationContainer<IProcessQueueFacade<TId>.ProcessDto> _processToExecuteBuffer;

        private IsolationContainer<IProcessQueueFacade<TId>.ProcessDto> _processContinueExecuteBuffer;

        private IsolationContainer<TId> _processExecutedBuffer;

        private TimeSpan ReserveTimeout { get; set; }
            = TimeSpan.FromSeconds(30);

        private bool TransactionIsRegistered { get; set; }

        private int BufferCapactity { get; set; }
             = 5;

        public ProcessQueueFacade(
            IDateTimeProvider dateTimeProvider,
            ITransactionManager transactionManager,
            IIsolationService isolationService,
            IProcessQueueProvider<TId> processQueueProvider,
            IProcessReservationProvider<TId> processReservationProvider)
        {
            _dateTimeProvider = dateTimeProvider;
            _transactionManager = transactionManager;
            _isolationService = isolationService;
            _processQueueProvider = processQueueProvider;
            _processReservationProvider = processReservationProvider;

            _registeredScopes = new HashSet<int>(2);
            _processToExecuteBuffer = new IsolationContainer<IProcessQueueFacade<TId>.ProcessDto>(2);
            _processContinueExecuteBuffer = new IsolationContainer<IProcessQueueFacade<TId>.ProcessDto>(2);
            _processExecutedBuffer = new IsolationContainer<TId>(2);
        }

        public void InitBufferCapacity(int capacity)
        {
            BufferCapactity = capacity;
        }

        public void SetReserveTimeout(TimeSpan reserveTimeout)
        {
            ReserveTimeout = reserveTimeout;
        }

        public void ProcessToExecute(IProcessQueueFacade<TId>.ProcessDto process)
        {
            RegisterScopeHandler();

            var scopeIndex = _isolationService.TryGetCurrentScopeInfo(out var scope)
                ? scope.ScopeIndex
                : IsolationContainer.TransactionIsolationIndex;
            _processToExecuteBuffer.Add(scopeIndex, process, BufferCapactity);
        }

        public void ProcessContinueExecute(IProcessQueueFacade<TId>.ProcessDto process)
        {
            RegisterScopeHandler();

            var scopeIndex = _isolationService.TryGetCurrentScopeInfo(out var scope)
                ? scope.ScopeIndex
                : IsolationContainer.TransactionIsolationIndex;
            _processContinueExecuteBuffer.Add(scopeIndex, process, BufferCapactity);
        }

        public void ProcessExecuted(TId id)
        {
            RegisterScopeHandler();

            var scopeIndex = _isolationService.TryGetCurrentScopeInfo(out var scope)
                ? scope.ScopeIndex
                : IsolationContainer.TransactionIsolationIndex;
            _processExecutedBuffer.Add(scopeIndex, id, BufferCapactity);
        }

        public async Task<bool> ProcessFromSelectorAsync(
            ICollection<IProcessQueueFacade<TId>.ProcessDto> ids, 
            DateTimeOffset reserveDate, 
            CancellationToken cancellationToken)
        {
            var reserveResult = await _processReservationProvider.TryReserveAsync(
                ids.Select(e => e.GetId()).ToArray(),
                reserveDate,
                cancellationToken);
            var isFull = await _processQueueProvider.ProduceAsync(
                ids.Select(e => new IProcessQueueProvider<TId>.MessageDto(e.ProcessRegistry, e.GetId())).ToArray(),
                cancellationToken);

            return isFull;
        }
        
        private void RegisterScopeHandler()
        {
            // 1) Прикрепление к TransactionScope.
            if (!TransactionIsRegistered)
            {
                if (!_transactionManager.TryGetCurrentTransaction(out var transaction))
                {
                    throw new Exception("Transaction required.");
                }                

                transaction.AddAfterCommitHandler(
                    this, 
                    static async (s, t) => 
                    {
                        var typedState = (ProcessQueueFacade<TId>)s;
                        await typedState.ExecuteAsync(t);

                        typedState._registeredScopes.Clear();
                        typedState._processToExecuteBuffer.Clear();
                        typedState._processContinueExecuteBuffer.Clear();
                        typedState._processExecutedBuffer.Clear();                        
                    }, 
                    static (_, _) => ValueTask.CompletedTask
                    );

                TransactionIsRegistered = true;
            }
            
            // 2) Прикрепление к isolation scope.
            if (
                _isolationService.TryGetCurrentScopeInfo(out var scope)
                && !_registeredScopes.Contains(scope.ScopeIndex))
            {
                _isolationService.RegisterManualCompensate(
                    this,
                    static (scopeIndex, state, t) =>
                    {
                        var typedState = (ProcessQueueFacade<TId>)state;

                        typedState._processToExecuteBuffer.ScopeCompensated(scopeIndex);
                        typedState._processContinueExecuteBuffer.ScopeCompensated(scopeIndex);
                        typedState._processExecutedBuffer.ScopeCompensated(scopeIndex);

                        return ValueTask.CompletedTask;
                    });

                _registeredScopes.Add(scope.ScopeIndex);
            }
        }

        private async ValueTask ExecuteAsync(CancellationToken cancellationToken)
        {
            var reserveTimeout = _dateTimeProvider.UtcNow + ReserveTimeout;

            if (_processToExecuteBuffer.All.Any())
            {
                await _processReservationProvider.ContinueReserveAsync(
                    _processToExecuteBuffer.All.Select(e => e.GetId()).ToArray(),
                    reserveTimeout,
                    cancellationToken);
                await _processQueueProvider.ProduceAsync(
                    _processToExecuteBuffer.All
                        .Select(e => new IProcessQueueProvider<TId>.MessageDto(
                            e.ProcessRegistry,
                            e.GetId())
                        )
                        .ToArray(),
                    cancellationToken);
            }

            if (_processContinueExecuteBuffer.All.Any())
            {
                await _processReservationProvider.ContinueReserveAsync(
                    _processContinueExecuteBuffer.All.Select(e => e.GetId()).ToArray(),
                    reserveTimeout,
                    cancellationToken);
                await _processQueueProvider.ProduceAsync(
                    _processContinueExecuteBuffer.All
                        .Select(e => new IProcessQueueProvider<TId>.MessageDto(
                            e.ProcessRegistry,
                            e.GetId())
                        )
                        .ToArray(),
                    cancellationToken);
            }

            if (_processExecutedBuffer.All.Any())
            {
                await _processReservationProvider.UnreserveAsync(
                    _processExecutedBuffer.All.ToArray(),
                    cancellationToken);
            }
        }        
    }
}
