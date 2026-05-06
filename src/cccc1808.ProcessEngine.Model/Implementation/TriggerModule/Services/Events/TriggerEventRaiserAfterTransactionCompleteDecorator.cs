using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
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

        private readonly Dictionary<Guid, ICollection<ITriggerEvent<TId>>> _sendBuffer;

        public TriggerEventRaiserAfterTransactionCompleteDecorator(
            ITriggerEventRaiser<TId> source,
            ITransactionManager transactionManager)
        {
            _source = source;
            _transactionManager = transactionManager;
            _sendBuffer = new Dictionary<Guid, ICollection<ITriggerEvent<TId>>>();
        }

        public ValueTask RaiseAsync(
            ICollection<ITriggerEvent<TId>> events,
            CancellationToken cancellationToken)
        {
            if (!_transactionManager.TryGetCurrentTransaction(out var transaction))
            {
                throw new InvalidOperationException("[Bug] Необходима транзакция.");
            }

            var key = Guid.NewGuid();
            _sendBuffer.Add(key, events);

            transaction.AddAfterCommitHandler(
                commitHandler: async (_) => 
                {
                    // Игнорируем cancelation token, чтобы выполнить отправку, даже если сервис останавливается.
                    // Если будет gracefull shutdown, то событие будет опубликовано, иначе событие потеряется.
                    await _source.RaiseAsync(
                        _sendBuffer.Values.SelectMany(e => e).ToArray(),
                        default);
                },
                roolbackHandler: (_) => 
                {
                    _sendBuffer.Remove(key);
                    return ValueTask.CompletedTask;
                }
                );

            return ValueTask.CompletedTask;
        }
    }
}
