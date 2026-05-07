using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Test2.TestGroup2.Infrastructure.Services
{
    internal class ParentProcessDataDbEntity
        : IProcessLinked<Guid>
    {       
        public Guid Id { get; set; }

        public Guid ProcessId { get; set; }

        public ParentProcessDataDbEntity() { }

        public ParentProcessDataDbEntity(Guid id, Guid processId)
        {
            Id = id;
            ProcessId = processId;
        }
    }
}
