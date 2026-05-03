using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Components
{
    public class WakeupComponent<TId> 
        : IWakeupComponent<TId>
    {
        public TId Id { get; }        

        public bool IsAsyncExecuting { get; set; }

        public bool HaveWakeupEntity { get; set; }

        public bool NeedUpdate { get; set; }

        public WakeupComponent(
            TId id, 
            bool isAsyncExecuting,
            bool haveWakeupEntity,
            bool needUpdate)
        {
            Id = id;
            IsAsyncExecuting = isAsyncExecuting;
            HaveWakeupEntity = haveWakeupEntity;
            NeedUpdate = needUpdate;
        }
    }
}
