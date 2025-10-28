using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto
{
    /// <summary>
    /// Тип процесса.
    /// </summary>
    /// <param name="ProcessType"></param>
    /// <param name="ProcessVersion"></param>
    public readonly record struct ProcessTypeDto(
        long ProcessType, 
        int ProcessVersion);
}
