using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events
{
    /// <summary>
    /// Kля производительности генерация отпрака <see cref="ITriggerEvent"/> идет без TransactionOutbox,
    /// поэтому сама публикация сообщения происходит после завершения транзакции.
    /// 
    /// Потеря события считается допустимой (хоть и маловероятной), 
    /// на этот случай для типа процесса создается страхующий триггер (см. Emergency trigger handler).
    /// </summary>
    public class TriggerEventRaiserAfterTransactionCompleteDecorator<TId>
        : ITriggerEventRaiser<TId>
    {
        private readonly ITriggerEventRaiser<TId> _source;
        private readonly ITransactionManager _transactionManager;
        private readonly IIsolationService _isolationService;

        private readonly HashSet<int> _registeredScopes;
        private readonly IsolationContainer<ITriggerEventRaiser<TId>.RaiseContainer> _sendBuffer;

        private bool TransactionIsRegistered { get; set; }

        public TriggerEventRaiserAfterTransactionCompleteDecorator(
            ITriggerEventRaiser<TId> source,
            ITransactionManager transactionManager,
            IIsolationService isolationService)
        {
            _source = source;
            _transactionManager = transactionManager;            
            _isolationService = isolationService;

            _registeredScopes = new HashSet<int>(10);
            _sendBuffer = new IsolationContainer<ITriggerEventRaiser<TId>.RaiseContainer>(10);
        }

        public ValueTask RaiseAsync(
            ICollection<ITriggerEventRaiser<TId>.RaiseContainer> events,
            CancellationToken cancellationToken)
        {
            if (!events.Any())
            {
                return ValueTask.CompletedTask;
            }

            RegisterScopeHandler();

            var scopeIndex = _isolationService.TryGetCurrentScopeInfo(out var scope)
                ? scope.ScopeIndex
                : IsolationContainer.TransactionIsolationIndex;
            // _sendBuffer.EncreaseCapacity(ScopeIndex, events.Count);
            _sendBuffer.AddRange(scopeIndex, events);
            
            return ValueTask.CompletedTask;
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
                        var typedState = (TriggerEventRaiserAfterTransactionCompleteDecorator<TId>)s;

                        // Игнорируем cancelation token, чтобы выполнить отправку, даже если сервис останавливается.
                        // Если будет gracefull shutdown, то событие будет опубликовано, иначе событие потеряется.
                        await typedState._source.RaiseAsync(
                            typedState._sendBuffer.All.ToArray(),
                            default);
                        typedState.Clear();
                    },
                    static (s, _) => 
                    {
                        var typedState = (TriggerEventRaiserAfterTransactionCompleteDecorator<TId>)s;
                        typedState.Clear();

                        return ValueTask.CompletedTask;
                    }
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
                    static (scopeIndex, s, t) =>
                    {
                        var typedState = (TriggerEventRaiserAfterTransactionCompleteDecorator<TId>)s;

                        typedState._sendBuffer.ScopeCompensated(scopeIndex);
                        return ValueTask.CompletedTask;
                    });

                _registeredScopes.Add(scope.ScopeIndex);
            }
        }

        public void ClearBuffer()
        {
            Clear();
            _source.ClearBuffer();
        }

        private void Clear() 
        {
            _sendBuffer.Clear();
        }        
    }
}
