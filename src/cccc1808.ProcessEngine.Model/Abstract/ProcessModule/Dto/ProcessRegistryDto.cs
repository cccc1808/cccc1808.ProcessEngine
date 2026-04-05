using System;
using System.Collections.Generic;
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
        short Priority)
    {
    }
}
