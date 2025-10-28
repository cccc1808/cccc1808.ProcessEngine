using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Registry
{
    /// <summary>
    /// Регистратор retry timer процесса.
    /// </summary>
    /// <param name="ProcessType"></param>
    public record ReTryTimerProcessRegistryDto(
        ProcessTypeDto ProcessType)
    {
    }
}
