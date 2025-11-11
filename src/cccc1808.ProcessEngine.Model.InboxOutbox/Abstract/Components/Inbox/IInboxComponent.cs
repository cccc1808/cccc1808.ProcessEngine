using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox
{
    public interface IInboxComponent<TId>
    {
        string Queue { get; }

        IList<IInboxMessageComponent<TId>> Messages { get; }

        #region InMemory

        int CurrentMessageIndex { get; set; }

        long ActiveMessagesCount { get; }

        #endregion
    }
}
