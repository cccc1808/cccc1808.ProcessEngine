using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;
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
                                // Активных сообщений нет - засыпаем.
                                if (inbox.ActiveMessagesCount == 0)
                                {
                                    _processSetter.SetStatus(e, ProcessStatusEnum.WaitEvent);
                                }
                                // Активные сообщения есть, но ни одно не загружено - обработка не требуется.
                                else
                                {
                                    // TODO: поместить в очередь
                                    _processSetter.StopAsyncProcessingSession(e);
                                }

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
            else if (inboxComponent.ActiveMessagesCount == inboxComponent.Messages.Count)
            {
                // Все сообщения обработаны - засыпаем.
                _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);
            }
            else 
            {
                // Есть необработанные сообщения, но загруженный батч обработан.
                _processSetter.StopAsyncProcessingSession(process);
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
                                // Активных сообщений нет.
                                if (outbox.ActiveMessagesCount == 0)
                                {
                                    // Пытаемся уснуть.
                                    _processSetter.SetStatus(e, ProcessStatusEnum.WaitEvent);
                                }
                                // Активные сообщения есть, но ни одно не загружено в текущей сессии.
                                else
                                {                                    
                                    _processSetter.StopAsyncProcessingSession(e);
                                    // TODO: поместить в очередь
                                }

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
            else if (outboxComponent.ActiveMessagesCount == outboxComponent.Messages.Count)
            {
                // Все сообщения обработаны - засыпаем.
                _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);
            }
            else
            {
                // Есть необработанные сообщения, но загруженный батч обработан.
                _processSetter.StopAsyncProcessingSession(process);
            }
        }        
    }
}
