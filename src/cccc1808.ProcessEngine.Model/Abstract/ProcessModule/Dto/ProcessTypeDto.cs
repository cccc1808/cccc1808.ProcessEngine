using System;
using System.Collections.Generic;
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
    {
        public override string ToString()
        {
            return $"{ProcessType}.{ProcessVersion}";
        }
    }
}
