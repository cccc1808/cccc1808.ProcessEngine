using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services
{
    /// <summary>
    /// Inbox process -> inbox handler.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class InboxSingleProcessHandler<TId> 
        : BaseSingleProcessHandler<TId>
    {
        private readonly OptionsDto _options;
        protected readonly IInboxSetter _inboxSetter;

        public InboxSingleProcessHandler(
            IIsolationService<TId> isolationService,
            IProcessRepository<TId> repository,
            ITriggerRepository<TId> triggerRepository,
            IProcessSetter setter,
            OptionsDto options,
            IInboxSetter inboxSetter)
            : base(
                  isolationService,
                  repository,
                  triggerRepository,
                  setter)
        {
            _options = options;
            _inboxSetter = inboxSetter;
        }

        protected override OptionsDto SingleOptions 
            => _options;

        protected override async ValueTask StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            var component = process.GetComponent<IInboxComponent<TId>>();

            // TODO: заготовка без изоляции.
            while (component.CurrentMessageIndex < component.Messages.Count)
            {
                var message = component.Messages[component.CurrentMessageIndex];

                await HandleMessageAsync(
                    process,
                    message,
                    cancellationToken
                    );

                _inboxSetter.InboxMessageProcessed(
                    process,
                    component,
                    message
                    );
            }
        }        

        protected abstract ValueTask HandleMessageAsync(
            IProcessContainer<TId> process,
            IMessageComponent<TId> message,
            CancellationToken cancellationToken);
    }
}
