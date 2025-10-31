using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Dto.Componenets
{
    public class InboxProcessComponent<TId>
    {
        public InboxProcessDataDbEntity<TId> Data { get; init; }

        public IList<InboxMessageDbEntity<TId>> Messages { get; init; }

        public int UnreadCount { get; init; }
    }
}
