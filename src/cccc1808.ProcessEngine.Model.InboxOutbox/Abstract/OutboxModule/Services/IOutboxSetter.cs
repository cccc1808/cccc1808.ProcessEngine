using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services
{
    public interface IOutboxSetter
    {
        /// <summary>
        /// Хендлер, после успешной обработки сообщения.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="process"></param>
        /// <param name="outboxComponent"></param>
        /// <param name="message"></param>
        void OutboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IOutboxComponent<TId> outboxComponent,
            IOutboxMessageComponent<TId> message
            );
    }
}
