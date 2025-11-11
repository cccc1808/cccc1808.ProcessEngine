using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract
{
    public interface IInboxOutboxSetter
    {
        ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup PrepareOutboxGroup<TId>(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group);

        void OutboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IOutboxComponent<TId> outboxComponent,
            IOutboxMessageComponent<TId> message
            );

        ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup PrepareInboxGroup<TId>(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group);

        void InboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IInboxComponent<TId> inboxComponent,
            IInboxMessageComponent<TId> message
            );        
    }
}
