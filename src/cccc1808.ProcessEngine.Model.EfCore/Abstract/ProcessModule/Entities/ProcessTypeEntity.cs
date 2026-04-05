using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities
{
    public class ProcessTypeEntity
        : IId<long>
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public short Version { get; set; }
    }
}
