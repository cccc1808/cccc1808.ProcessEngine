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
        in ProcessTypeUniqueDto Unique,
        in ProcessTypeMetadata Metadata);

    /// <summary>
    /// Уникальный идентефикатор типа и версии процесса.
    /// </summary>
    public readonly record struct ProcessTypeUniqueDto(
        in ProcessTypeDto ProcessType,
        short Priority)
        : IEqualityComparer<ProcessTypeUniqueDto>
    {
        public bool Equals(ProcessTypeUniqueDto x, ProcessTypeUniqueDto y)
        {
            return 
                x.ProcessType.Equals(x.ProcessType, y.ProcessType) 
                && x.Priority == y.Priority;
        }

        public int GetHashCode([DisallowNull] ProcessTypeUniqueDto obj)
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

    public readonly record struct ProcessTypeMetadata(
        bool IsSignleExecuteProcess);
}
