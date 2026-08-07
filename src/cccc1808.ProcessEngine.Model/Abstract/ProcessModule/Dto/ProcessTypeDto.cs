using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto
{
    /// <summary>
    /// Тип процесса.
    /// </summary>
    /// <param name="ProcessType">Ключ типа процесса.</param>
    /// <param name="ProcessVersion">Версия типа процесса.</param>
    public readonly record struct ProcessTypeDto(
        long ProcessType,
        int ProcessVersion)
        : IEqualityComparer<ProcessTypeDto>
    {
        public bool Equals(ProcessTypeDto x, ProcessTypeDto y)
        {
            return 
                x.ProcessType == y.ProcessType 
                && x.ProcessVersion == y.ProcessVersion;
        }

        public int GetHashCode([DisallowNull] ProcessTypeDto obj)
        {
            return HashCode.Combine(
                obj.ProcessType, 
                obj.ProcessVersion);
        }
    }
}
