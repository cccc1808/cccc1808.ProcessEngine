using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Entities
{
    /// <summary>
    /// TODO: Можно добавить поля (ParentProcessId, ParentProcessLocked) в SchemaProcessDataDbEntity, чтобы было в одной таблице.
    /// </summary>
    public class ParentChildProcessDbEntity : 
        IId<Guid>, 
        IProcessLinked<Guid>
    {
        public Guid Id { get; set; }

        public long TimeStamp { get; set; }

        public Guid ProcessId { get; set; }

        public string? TriggerKey { get; set; }

        public bool IsActive { get; set; }

        public Guid ChildProcessId { get; set; }

        public ParentChildProcessDbEntity(
            Guid id, 
            long timeStamp,
            Guid processId,
            string? triggerKey,
            bool isActive,
            Guid childProcessId)
        {
            Id = id;
            TimeStamp = timeStamp;
            ProcessId = processId;
            TriggerKey = triggerKey;
            IsActive = isActive;
            ChildProcessId = childProcessId;
        }
    }
}
