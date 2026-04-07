using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    public class SoftTimeoutComponent : ISoftTimeoutComponent
    {
        public DateTimeOffset? StopDate { get; }


        public SoftTimeoutComponent(DateTimeOffset? stopDate)
        {
            StopDate = stopDate;
        }
    }
}
