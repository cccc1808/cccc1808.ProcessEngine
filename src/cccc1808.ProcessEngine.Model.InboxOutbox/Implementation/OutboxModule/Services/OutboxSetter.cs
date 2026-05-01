using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Services;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.OutboxModule.Services
{
    public class OutboxSetter 
        : IOutboxSetter
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProcessSetter _processSetter;
        private readonly OutboxRegistryDto _outboxRegistry;

        public OutboxSetter(
            IDateTimeProvider dateTimeProvider,
            IProcessSetter processSetter,
            OutboxRegistryDto outboxRegistry)
        {
            _dateTimeProvider = dateTimeProvider;
            _processSetter = processSetter;
            _outboxRegistry = outboxRegistry;
        }

        public void OutboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IOutboxComponent<TId> outboxComponent,
            IOutboxMessageComponent<TId> message)
        {
            if (process.CurrentSession.CurrentSessionHaveError)
            {
                return;
            }

            message.IsActive = false;
            message.SendDate = _dateTimeProvider.UtcNow;
            outboxComponent.ProcessedCount++;

            if (outboxComponent.ProcessedCount < outboxComponent.Messages.Count)
            {
                // Есть еще сообщение в батче.
            }
            else
            {
                // Батч обработан.
                // Если в БД еще есть активные сообщения, то это обнаружит OutboxMessageWakeupHandler.
                _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);
            }

            // Обновляем смещение для триггера.
            var streamComponent = process.GetComponent<IStreamTriggerComponent>();
            streamComponent.UpdateMaxTimestamp(_outboxRegistry.TriggerChannelName, message.OrderId);
        }
    }
}
