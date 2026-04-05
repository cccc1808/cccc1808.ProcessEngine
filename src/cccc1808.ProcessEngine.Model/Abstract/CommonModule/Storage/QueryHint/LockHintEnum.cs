using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint
{
    /// <summary>
    /// Указатели для запроса
    /// </summary>
    public enum LockHintEnum
    {
        No = 0,

        ForNoKeyUpdate,
        ForNoKeyUpdateAndSkipLocked,
        ForShare,
        ForShareAndSkipLocked,
    }
}
