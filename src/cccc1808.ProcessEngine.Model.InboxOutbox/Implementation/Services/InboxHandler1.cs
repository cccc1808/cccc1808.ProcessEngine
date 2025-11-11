using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.Services
{
    public abstract class InboxHandler1<TId> 
        : BaseSingleProcessHandler<TId>
    {
        private readonly OptionsDto _options;
        protected readonly IInboxOutboxSetter _inboxOutboxSetter;

        public InboxHandler1(
            IIsolationService isolationService,
            IProcessRepository<TId> repository,
            IProcessSetter setter,
            OptionsDto options)
            : base(
                  isolationService,
                  repository,
                  setter)
        {
            _options = options;     
        }

        protected override OptionsDto SingleOptions 
            => _options;

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group, 
            CancellationToken cancellationToken)
        {
            group = _inboxOutboxSetter.PrepareInboxGroup(group);
            await base.StepRangeAsync(group, cancellationToken);
        }

        protected override async ValueTask StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            var component = process.GetComponent<IInboxComponent<TId>>();
            var message = component.Messages[component.CurrentMessageIndex];

            await HandleMessageAsync(
                process,
                message,
                cancellationToken
                );

            _inboxOutboxSetter.InboxMessageProcessed(
                process,
                component,
                message
                );
        }        

        protected abstract ValueTask HandleMessageAsync(
            IProcessContainer<TId> process,
            IMessageComponent<TId> message,
            CancellationToken cancellationToken);
    }
}
