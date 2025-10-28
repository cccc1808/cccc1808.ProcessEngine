using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto
{
    public enum ProcessStatusEnum
    {
        /// <summary>
        /// Процесс находиться в асинхронной обработке.
        /// </summary>
        AsyncExecute = 0,

        /// <summary>
        /// Процесс ожидает действия извне.
        /// </summary>
        WaitEvent = 1,

        /// <summary>
        /// Процесс завершен.
        /// </summary>
        Complete = 2
    }
}
