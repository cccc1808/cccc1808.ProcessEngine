using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    public class ProcessWakeUpDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public TId ProcessId { get; set; }
        public ProcessDbEntity<TId> Process { get; set; }

        /// <summary>
        /// Дата обновления.
        /// </summary>
        public DateTimeOffset TimeStamp { get; set; }

        /// <summary>
        /// Отображает, что процесс находится в состоянии асинхронной обработки.
        /// </summary>
        public bool IsAsyncExecuting { get; set; }

        /// <summary>
        /// Прибризительно отображает значение таймера процесса.
        /// </summary>
        public DateTimeOffset TimerDate { get; set; }
    }
}
