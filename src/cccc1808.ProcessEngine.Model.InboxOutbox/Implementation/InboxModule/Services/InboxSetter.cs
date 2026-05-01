using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Implementation.InboxModule.Services
{
    public class InboxSetter
        : IInboxSetter
    {        
        private readonly IProcessSetter _processSetter;

        public InboxSetter(
            IProcessSetter processSetter)
        {
            _processSetter = processSetter;
        }        

        public void InboxMessageProcessed<TId>(
            IProcessContainer<TId> process,
            IInboxComponent<TId> inboxComponent,
            IInboxMessageComponent<TId> message)
        {
            if (process.CurrentSession.CurrentSessionHaveError)
            {
                return;
            }

            message.IsActive = false;
            inboxComponent.CurrentMessageIndex++;

            if (inboxComponent.CurrentMessageIndex < inboxComponent.Messages.Count)
            {
                // Есть еще сообщение в батче.                
            }
            // Батч обработан - пытаемся устнуть.
            else
            {

                // Батч обработан.
                // Если в БД еще есть активные сообщения, то это обнаружит InboxMessageWakeupHandler.
                _processSetter.SetStatus(process, ProcessStatusEnum.WaitEvent);
            }
        }              
    }
}
