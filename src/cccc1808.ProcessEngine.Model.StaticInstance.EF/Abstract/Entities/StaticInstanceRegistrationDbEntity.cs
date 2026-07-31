using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Entities
{
    public class StaticInstanceRegistrationDbEntity<TId> : IId<TId>
    {
        public TId Id { get; set; }

        public long ProcessType { get; set; }

        public string InstanceKey { get; set; }
        
        public TId ProcessId { get; set; }

        public StaticInstanceRegistrationDbEntity(
            TId id, 
            long processType, 
            string instanceKey, 
            TId processId)
        {
            Id = id;
            ProcessType = processType;
            InstanceKey = instanceKey;
            ProcessId = processId;
        }
    }
}
