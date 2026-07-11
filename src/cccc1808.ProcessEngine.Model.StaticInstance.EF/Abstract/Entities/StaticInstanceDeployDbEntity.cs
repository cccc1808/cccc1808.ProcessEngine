using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities
{
    public class StaticInstanceDeployDbEntity<TId> : IId<TId>
    {
        public TId Id { get; set; }

        public short Version { get; set; }

        public StaticInstanceDeployDbEntity(
            TId id, 
            short version)
        {
            Id = id;
            Version = version;
        }
    }
}
