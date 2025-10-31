using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    /// <summary>
    /// Мягкий timeout для долгих операциий.
    /// Предпологает присотановку обработки данных на текущем этапе и продожение обработки в будущем.
    /// </summary>
    public interface ISoftTimeoutComponent
    {
        void StartScope();

        bool CheckTimeout();
    }
}
