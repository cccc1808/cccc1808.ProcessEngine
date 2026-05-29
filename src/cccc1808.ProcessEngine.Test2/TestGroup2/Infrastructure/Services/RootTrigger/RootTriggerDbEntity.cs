using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services.RootTrigger
{
    public class RootTriggerDbEntity
        : IId<Guid>,
        IProcessLinked<Guid>
    {
        public Guid Id { get; set; }

        public Guid ProcessId { get; set; }

        public bool IsFirst { get; set; }

        public Guid? RootTriggerId { get; set; }

        public Guid? ChildTriggerId { get; set; }

        [Obsolete]
        public RootTriggerDbEntity() { }

        public RootTriggerDbEntity(
            Guid id,
            Guid processId)
        {
            Id = id;
            ProcessId = processId;
            IsFirst = true;
            RootTriggerId = null;
            ChildTriggerId = null;
        }
    }
}
