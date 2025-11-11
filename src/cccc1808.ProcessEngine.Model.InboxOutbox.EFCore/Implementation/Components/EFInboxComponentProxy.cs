using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Components.Inbox;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.Components
{
    public class EFInboxComponentProxy<TId>
        : IInboxComponent<TId>
    {
        public InboxProcessDataDbEntity<TId> DbEntity { get; }

        public string Queue => DbEntity.Queue.Name;

        public IList<IInboxMessageComponent<TId>> Messages { get; }

        public int CurrentMessageIndex { get; set; }

        public long ActiveMessagesCount { get; }

        public EFInboxComponentProxy(
            InboxProcessDataDbEntity<TId> dbEntity,
            IList<IInboxMessageComponent<TId>> messages,
            int activeMessagesCount)
        {
            DbEntity = dbEntity;
            Messages = messages;
            ActiveMessagesCount = activeMessagesCount;
            CurrentMessageIndex = 0;
        }
    }
}
