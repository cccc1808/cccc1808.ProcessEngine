using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    /// <summary>
    /// Компонент для обнаружения зацикливания процесса.
    /// </summary>
    internal class StepByStepCycleDetectComponent
    {
        public int StepCount { get; set; }
    }
}
