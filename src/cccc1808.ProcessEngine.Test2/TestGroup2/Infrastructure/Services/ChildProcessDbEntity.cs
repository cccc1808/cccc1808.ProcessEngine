using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services
{
    internal class ChildProcessDbEntity
        : IProcessLinked<Guid>
    {
        public Guid Id { get; set; }

        public Guid ProcessId { get; set; }

        public Guid ParentProcessId { get; set; }
        public Guid? ActiveParentProcessId { get; set; }

        public string ParentTriggerKey { get; set; }

        public ChildProcessDbEntity(
            Guid processId,
            Guid parentProcessId,
            Guid? activeParentProcessId,
            string parentTriggerKey)
        {
            ProcessId = processId;
            ParentProcessId = parentProcessId;
            ActiveParentProcessId = activeParentProcessId;
            ParentTriggerKey = parentTriggerKey;
        }
    }
}
