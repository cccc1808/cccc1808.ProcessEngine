using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto
{
    public readonly record struct ProcessInstanceInfoDto<TId>(
        ProcessIdDto<TId> Id,
        ProcessTypeDto ProcessType,
        short Priority
        )
    {
        /// <summary>
        /// Для сортировки/группировки по критерию: (Priority, ProcessType.ProcessType, ProcessType.Version).
        /// </summary>
        public class PriorityComparer
            : Comparer<ProcessInstanceInfoDto<TId>>
        {
            public override int Compare(ProcessInstanceInfoDto<TId> x, ProcessInstanceInfoDto<TId> y)
            {
                return Comparer<(short, long, int)>.Default.Compare(
                    (x.Priority, x.ProcessType.ProcessType, x.ProcessType.ProcessVersion),
                    (y.Priority, y.ProcessType.ProcessType, y.ProcessType.ProcessVersion)
                    );
            }
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ProcessType.ProcessType,
                ProcessType.ProcessVersion,
                Id);
        }
    }
}
