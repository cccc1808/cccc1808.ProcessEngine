using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services;

namespace cccc1808.ProcessEngine.Test2.TestGroup4.Infrastructure.Services
{
    internal class TestInboxBody
        : InboxSingleProcessHandler<Guid>
    {
        public TestInboxBody(
            IIsolationService isolationService, 
            IProcessRepository<Guid> repository, 
            ITriggerRepository<Guid> triggerRepository,
            IProcessSetter setter, 
            OptionsDto options, 
            IInboxSetter inboxSetter
            ) 
            : base(
                  isolationService, 
                  repository,
                  triggerRepository,
                  setter,
                  options,
                  inboxSetter)
        {
        }

        protected override ValueTask HandleMessageAsync(
            IProcessContainer<Guid> process,
            IMessageComponent<Guid> message, 
            CancellationToken cancellationToken)
        {
            var messagesState = process.GetComponent<MessagesStateComponent<MessageState>>();
            var messageState = messagesState.State[message.Key];

            messageState.BuisnessDbEntity.Counter++;
            return ValueTask.CompletedTask;
        }
    }
}
