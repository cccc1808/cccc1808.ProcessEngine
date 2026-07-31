using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Component.ActionComponent
{
    public class ServiceTaskActionState 
        : ITokenActionStateComponent
    {
        public string Id { get; set; }

        public StatusEnum Status { get; set; }

        [Obsolete]
        public ServiceTaskActionState() 
        {
            Id = default!;
        }

        public ServiceTaskActionState(
            string id,
            StatusEnum status)
        {
            Id = id;
            Status = status;
        }

        public enum StatusEnum 
        {
            NoActivated,
            Executing,
            Complete
        }
    }
}
