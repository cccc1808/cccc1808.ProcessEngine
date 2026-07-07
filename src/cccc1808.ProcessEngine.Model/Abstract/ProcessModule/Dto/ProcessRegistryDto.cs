using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto
{
    /// <summary>
    /// Регистрация версии процесса для обработки.
    /// ! Эти данные нельзя менять не поднимая версию процесса.
    /// </summary>
    /// <param name="ProcessType"></param>
    /// <param name="Priority">
    /// Какой приоритет нужно обрабатывать
    /// (позволяет обрабатывать разные приоритеты разными нодами).
    /// </param>
    /// <param name="UseSignal">Использует ли процессы коды сигналов.</param>
    public record ProcessRegistryDto(
        ProcessTypeDto ProcessType,
        short Priority,
        bool UseSignal)
    {
        public ProcessRegistryDto(
            long ProcessType,
            int ProcessVersion,
            short Priority,
            bool UseSignal)
            : this(
                  new ProcessTypeDto(ProcessType, ProcessVersion),
                  Priority, 
                  UseSignal)
        {
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ProcessType.GetHashCode(), 
                Priority);
        }
    }
}
