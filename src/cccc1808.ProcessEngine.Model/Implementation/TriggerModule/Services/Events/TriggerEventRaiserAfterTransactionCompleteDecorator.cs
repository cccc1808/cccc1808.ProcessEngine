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
    public class TriggerEventRaiserAfterTransactionCompleteDecorator<TId> : ITriggerEventRaiser<TId>
    {
        private readonly ITriggerEventRaiser<TId> _source;
        private readonly ITransactionManager _transactionManager;
        private readonly IIsolationService _isolationService;
        
        private readonly Dictionary<Guid, ICollection<ITriggerEventRaiser<TId>.RaiseContainer>> _sendBuffer;

        private bool HandlerRegistered { get; set; }

        public TriggerEventRaiserAfterTransactionCompleteDecorator(
            ITriggerEventRaiser<TId> source,
            ITransactionManager transactionManager,
            IIsolationService isolationService)
        {
            _source = source;
            _transactionManager = transactionManager;            
            _isolationService = isolationService;
            _sendBuffer = new Dictionary<Guid, ICollection<ITriggerEventRaiser<TId>.RaiseContainer>>();
        }

        public ValueTask RaiseAsync(
            ICollection<ITriggerEventRaiser<TId>.RaiseContainer> events,
            CancellationToken cancellationToken)
        {
            if (!_transactionManager.TryGetCurrentTransaction(out var transaction))
            {
                throw new InvalidOperationException("[Bug] Необходима транзакция.");
            }

            if (!HandlerRegistered)
            {
                // 1) Привязка к транзакции.
                transaction.AddAfterCommitHandler(
                    commitHandler: async (_) =>
                    {
                        // Игнорируем cancelation token, чтобы выполнить отправку, даже если сервис останавливается.
                        // Если будет gracefull shutdown, то событие будет опубликовано, иначе событие потеряется.
                        await _source.RaiseAsync(
                            _sendBuffer.Values.SelectMany(e => e).ToArray(),
                            default);
                    },
                    roolbackHandler: CompensateTransactionHandler
                    );

                HandlerRegistered = true;
            }

            var key = Guid.NewGuid();
            _sendBuffer.Add(key, events);
            
            // Если находимся в scope изоляции, то регистрируем событие на случай компенсации scope.
            if (_isolationService.InScope)
            {
                // 2) Привязка к текущему scope изоляции.
                _isolationService.RegisterManualCompensate(
                    (t) => 
                    {
                        _sendBuffer.Remove(key);
                        return ValueTask.CompletedTask;
                    }
                    );
            }

            return ValueTask.CompletedTask;
        }

        private ValueTask CompensateTransactionHandler(CancellationToken _) 
        {
            _sendBuffer.Clear();
            return ValueTask.CompletedTask;
        }
    }
}
