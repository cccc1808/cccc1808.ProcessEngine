using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions
{
    public interface IProcessContainerConditions<TId>
    {
        /// <summary>
        /// Условие асинхронной обработки процесса.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        (
            object? _no, 
            IInMemoryCondition<IProcessContainer<TId>> Memory
            ) AsyncExecute
        { get; }

        /// <summary>
        /// Наличие ошибки в процессе  (Retry или HaveError).
        /// </summary>
        IInMemoryCondition<IProcessContainer<TId>> HaveError { get; }
    }
}
