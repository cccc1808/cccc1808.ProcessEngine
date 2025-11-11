using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox
{
    public interface IOutboxComponent<TId>
    {
        string Queue { get; }

        IList<IOutboxMessageComponent<TId>> Messages { get; }

        long ActiveMessagesCount { get; }

        int ProcessedCount { get; set; }
    }
}
