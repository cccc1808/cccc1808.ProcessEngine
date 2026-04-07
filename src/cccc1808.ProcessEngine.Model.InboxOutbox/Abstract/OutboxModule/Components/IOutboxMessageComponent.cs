using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.CommonModule.Components;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components
{
    public interface IOutboxMessageComponent<TId>
        : IMessageComponent<TId>
    {
        public DateTimeOffset? SendDate { get; set; }
    }
}
