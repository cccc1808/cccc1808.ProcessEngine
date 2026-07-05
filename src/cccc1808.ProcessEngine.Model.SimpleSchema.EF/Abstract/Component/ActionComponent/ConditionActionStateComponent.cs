using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent
{
    public class ConditionActionStateComponent 
        : ITokenActionStateComponent
    {
        public string Id { get; set; }

        public StatusEnum Status { get; set; }

        public bool IgnoreSignal { get; set; }

        [Obsolete]
        public ConditionActionStateComponent()
        {
            Id = default!;
        }

        public ConditionActionStateComponent(
            string id,
            StatusEnum status)
        {
            Id = id;
            Status = status;
            IgnoreSignal = false;
        }

        public enum StatusEnum
        {
            NoActivated,
            WaitSignal,
            CheckCondition,
            Complete,
        }
    }
}
