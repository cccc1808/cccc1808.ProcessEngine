using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.WakeupModule.Entities
{
    public class ProcessWakeUpDbEntity<TId>
        : IId<TId>, 
        IProcessLinked<TId>
    {
        public TId Id { get; set; } = default!;

        public TId ProcessId { get; set; } = default!;
        public ProcessDbEntity<TId> Process { get; set; } = default!;

        /// <summary>
        /// Отображает, что процесс находится в состоянии асинхронной обработки.
        /// </summary>
        public bool IsAsyncExecuting { get; set; }

        public ProcessWakeUpDbEntity() 
        {
        }

        public ProcessWakeUpDbEntity(
            TId id,
            TId processId,
            bool isAsyncExecuting)
        {
            Id = id;
            ProcessId = processId;
            IsAsyncExecuting = isAsyncExecuting;
        }
    }
}
