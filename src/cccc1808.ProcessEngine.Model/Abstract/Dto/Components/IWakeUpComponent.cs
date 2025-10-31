using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    public interface IWakeUpComponent
    {
        #region persist        

        DateTimeOffset Timestamp { get; set; }

        bool IsAsyncExecuting { get; set; }

        DateTimeOffset TimerDate { get; set; }

        #endregion

        #region inmemory

        DateTimeOffset SessionStartTimeStamp { get; }

        bool InAsyncExecuting { get; set; }

        bool NeedUpdate { get; set; }

        #endregion
    }
}
