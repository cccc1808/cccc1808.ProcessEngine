using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services
{
    public interface IInboxSetter
    {
        void InboxMessageProcessed<TId>(
           IProcessContainer<TId> process,
           IInboxComponent<TId> inboxComponent,
           IInboxMessageComponent<TId> message
           );      
    }
}
