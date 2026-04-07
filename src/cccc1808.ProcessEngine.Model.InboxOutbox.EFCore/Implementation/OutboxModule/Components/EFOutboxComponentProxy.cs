using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Components
{
    public class EFOutboxComponentProxy<TId>
        : IOutboxComponent<TId>
    {
        public OutboxProcessDataDbEntity<TId> DbEntity { get; }

        public string Queue => DbEntity.Queue.Name;

        public IList<IOutboxMessageComponent<TId>> Messages { get; }

        public int ProcessedCount { get; set; }

        public EFOutboxComponentProxy(
            OutboxProcessDataDbEntity<TId> dbEntity,
            IList<IOutboxMessageComponent<TId>> messages)
        {
            DbEntity = dbEntity;
            Messages = messages;
            ProcessedCount = 0;
        }
    }
}
