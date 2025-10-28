using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    public interface IWakeUpComponent
    {
        DateTimeOffset SessionStartTimeStamp { get; }

        DateTimeOffset Timestamp { get; set; }

        bool IsAsyncExecuting { get; set; }

        DateTimeOffset TimerDate { get; set; }        

        bool NeedUpdate { get; set; }
    }
}
