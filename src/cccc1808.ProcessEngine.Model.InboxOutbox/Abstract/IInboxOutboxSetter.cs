using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract
{
    public interface IInboxOutboxSetter
    {
        /// <summary>
        /// Подготовить группу к асинхронной обработке.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="group"></param>
        /// <returns></returns>
        ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup PrepareOutboxGroup<TId>(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group);

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

        /// <summary>
        /// Подготовить группу к асинхронной обработке.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        /// <param name="group"></param>
        /// <returns></returns>
        ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup PrepareInboxGroup<TId>(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group);

        void InboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IInboxComponent<TId> inboxComponent,
            IInboxMessageComponent<TId> message
            );        
    }
}
