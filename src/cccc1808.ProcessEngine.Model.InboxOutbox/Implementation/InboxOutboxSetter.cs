using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation
{
    public class InboxOutboxSetter
        : IInboxOutboxSetter
    {
        private readonly IProcessSetter _processSetter;

        public InboxOutboxSetter(
            IProcessSetter processSetter)
        {
            _processSetter = processSetter;
        }

        public ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup PrepareInboxGroup<TId>(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group)
        {
            return new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                group.Group
                    .Values
                    .Where(
                        e =>
                        {
                            var inbox = e.GetComponent<IInboxComponent<TId>>();

                            // Загружено хотя бы одно сообщение
                            if (inbox.Messages.Any())
                            {
                                return true;
                            }
                            else
                            {
                                // Сообщений нет или не загружены.
                                // Пытаемся уснуть.
                                _processSetter.SetStatus(e, ProcessStatusEnum.WaitEvent);
                                return false;
                            }
                        }
                        )
                    .ToDictionary(e => e.Process.Info.Id, e => e)
                );
        }

        public void InboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IInboxComponent<TId> inboxComponent,
            IInboxMessageComponent<TId> message)
        {
            if (process.CurrentSession.HaveError)
            {
                return;
            }

            message.IsActive = false;            

            if (inboxComponent.CurrentMessageIndex < inboxComponent.Messages.Count)
            {
                // Есть еще сообщение в батче.
                inboxComponent.CurrentMessageIndex++;
            }
            // Батч обработан - пытаемся устнуть.
            else
            {

                // Батч обработан.
                // Если в БД еще есть активные сообщения, то это обнаружит InboxMessageWakeupHandler.
                _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);
            }
        }

        public ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup PrepareOutboxGroup<TId>(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group)
        {
            return new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                group.Group
                    .Values
                    .Where(
                        e =>
                        {
                            var outbox = e.GetComponent<IOutboxComponent<TId>>();

                            // Загружено хотя бы одно сообщение
                            if (outbox.Messages.Any())
                            {
                                // Обработка нужна.
                                return true;
                            }
                            else
                            {
                                // Сообщений нет или не загружены.
                                // Пытаемся уснуть.
                                _processSetter.SetStatus(e, ProcessStatusEnum.WaitEvent);

                                return false;
                            }
                        }
                        )
                    .ToDictionary(e => e.Process.Info.Id, e => e)
                );
        }

        public void OutboxMessageProcessed<TId>(
            IProcessContainer<TId> process, 
            IOutboxComponent<TId> outboxComponent, 
            IOutboxMessageComponent<TId> message)
        {
            if (process.CurrentSession.HaveError)
            {
                return;
            }

            message.IsActive = false;
            message.SendDate = DateTimeOffset.UtcNow;
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
        }        
    }
}
