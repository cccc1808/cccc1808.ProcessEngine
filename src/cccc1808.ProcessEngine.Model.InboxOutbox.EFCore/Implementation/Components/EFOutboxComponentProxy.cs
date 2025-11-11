using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Outbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.Components
{
    public class EFOutboxComponentProxy<TId>
        : IOutboxComponent<TId>
    {
        public OutboxProcessDataDbEntity<TId> DbEntity { get; }

        public string Queue => DbEntity.Queue.Name;

        public IList<IOutboxMessageComponent<TId>> Messages { get; }

        public long ActiveMessagesCount { get; }

        public int ProcessedCount { get; set; }

        public EFOutboxComponentProxy(
            OutboxProcessDataDbEntity<TId> dbEntity,
            IList<IOutboxMessageComponent<TId>> messages,
            long activeMessagesCount)
        {
            DbEntity = dbEntity;
            Messages = messages;
            ActiveMessagesCount = activeMessagesCount;
            ProcessedCount = 0;
        }
    }
}
