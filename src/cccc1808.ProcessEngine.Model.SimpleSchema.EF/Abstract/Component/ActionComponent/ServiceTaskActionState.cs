using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent
{
    public class ServiceTaskActionState 
        : ITokenActionStateComponent
    {
        public string Id { get; set; }

        public bool IsComplete { get; set; }

        [Obsolete]
        public ServiceTaskActionState() 
        {
            Id = default!;
            IsComplete = false;
        }

        public ServiceTaskActionState(
            string id,
            bool isComplete)
        {
            Id = id;
            IsComplete = isComplete;
        }
    }
}
