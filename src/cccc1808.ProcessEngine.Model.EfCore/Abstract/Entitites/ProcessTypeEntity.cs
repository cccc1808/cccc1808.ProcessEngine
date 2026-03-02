using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    public class ProcessTypeEntity
        : IId<long>
    {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public short Version { get; set; }
    }
}
