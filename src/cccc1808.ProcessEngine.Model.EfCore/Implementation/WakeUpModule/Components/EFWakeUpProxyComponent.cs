using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.WakeupModule.Components
{
    public class EFWakeupProxyComponent<TId>
        : IWakeupComponent
    {
        public ProcessWakeupDbEntity<TId> DbEntity { get; }

        public bool IsAsyncExecuting
        {
            get => DbEntity.IsAsyncExecuting;
            set => DbEntity.IsAsyncExecuting = value;
        }

        public bool NeedUpdate { get; set; }

        public bool InAsyncExecuting { get; set; }

        public bool HandlerResult { get; set; }

        public EFWakeupProxyComponent(
            ProcessWakeupDbEntity<TId> dbEntity,
            bool inAsyncExecuting)
        {
            DbEntity = dbEntity;
            InAsyncExecuting = inAsyncExecuting;
        }
    }
}
