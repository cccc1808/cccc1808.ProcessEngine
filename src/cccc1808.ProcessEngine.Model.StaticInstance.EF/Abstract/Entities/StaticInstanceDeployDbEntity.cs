using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities
{
    public class StaticInstanceDeployDbEntity : IId<short>
    {
        public short Id { get; set; }

        public short Version { get; set; }

        public StaticInstanceDeployDbEntity(
            short id, 
            short version)
        {
            Id = id;
            Version = version;
        }
    }
}
