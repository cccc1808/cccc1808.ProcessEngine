using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Components
{
    public class EFInboxComponentProxy<TId>
        : IInboxComponent<TId>
    {
        public InboxProcessDataDbEntity<TId> DbEntity { get; }

        public string Queue => DbEntity.Queue.Name;

        public IList<IInboxMessageComponent<TId>> Messages { get; }

        public int CurrentMessageIndex { get; set; }

        public EFInboxComponentProxy(
            InboxProcessDataDbEntity<TId> dbEntity,
            IList<IInboxMessageComponent<TId>> messages)
        {
            DbEntity = dbEntity;
            Messages = messages;
            CurrentMessageIndex = 0;
        }
    }
}
