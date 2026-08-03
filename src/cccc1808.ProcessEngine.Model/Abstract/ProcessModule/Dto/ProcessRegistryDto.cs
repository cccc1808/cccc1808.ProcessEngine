using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto
{
    /// <summary>
    /// Регистрация версии процесса для обработки.
    /// </summary>
    /// <param name="ProcessType"></param>
    /// <param name="Priority">
    /// Какой приоритет нужно обрабатывать
    /// (позволяет обрабатывать разные приоритеты разными нодами).
    /// </param>
    public record ProcessRegistryDto(
        ProcessTypeDto ProcessType,
        short Priority,
        bool IsSignleExecuteProcess)
        : IEqualityComparer<ProcessRegistryDto>
    {
        public bool Equals(ProcessRegistryDto? x, ProcessRegistryDto? y)
        {
            if (x is not null)
            {
                return x.Equals(y);
            }
            if (y is not null)
            {
                return y.Equals(x);
            }

            return true;
        }

        public int GetHashCode([DisallowNull] ProcessRegistryDto obj)
        {
            return obj.GetHashCode();
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ProcessType.GetHashCode(), 
                Priority);
        }        
    }
}
