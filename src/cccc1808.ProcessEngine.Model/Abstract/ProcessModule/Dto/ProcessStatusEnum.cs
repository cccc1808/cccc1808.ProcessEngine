using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto
{
    /// <summary>
    /// Статусы асинхронной обработки процесса.
    /// </summary>
    public enum ProcessStatusEnum
    {
        /// <summary>
        /// Процесс находиться в асинхронной обработке.
        /// </summary>
        AsyncExecute = 0,

        /// <summary>
        /// Процесс ожидает внешнего сигнала (Асинхронная обработка не выполнятся).
        /// * Retry
        /// * Ожидание поступления событий.
        /// * Ожидания дочерних процессов.
        /// </summary>
        WaitEvent = 1,

        /// <summary>
        /// Процесс завершен.
        /// </summary>
        Complete = 2
    }
}
