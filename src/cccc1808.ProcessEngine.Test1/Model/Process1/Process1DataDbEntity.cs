using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Test1.Model.Process1
{
    public class Process1DataDbEntity
        : IId<Guid>,
        IProcessLinked<Guid>
    {
        public Guid Id { get; set; }

        public Guid ProcessId { get; set; }
        public ProcessDbEntity<Guid> Process { get; set; }

        public int Counter { get; set; }

        public StatesEnum States { get; set; }
        

        public enum StatesEnum 
        {
            _1,
            _2,
            _3,
        }
    }
}
