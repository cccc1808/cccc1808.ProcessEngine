using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox
{
    public interface IOutboxMessageComponent<TId>
        : IMessageComponent<TId>
    {
        public DateTimeOffset? SendDate { get; set; }
    }
}
