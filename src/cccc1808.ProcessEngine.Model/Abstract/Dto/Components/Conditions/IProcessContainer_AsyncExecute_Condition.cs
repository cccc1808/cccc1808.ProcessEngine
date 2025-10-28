using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions
{
    /// <summary>
    /// Условие асинхронной обработки процесса.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class IProcessContainer_AsyncExecute_Condition<TId>
        : IInMemoryCondition<IProcessContainer<TId>, object?>
    {
        public bool Check(
            IProcessContainer<TId> source,
            object? parameters)
        {
            return 
                source.Process.Status == ProcessStatusEnum.AsyncExecute
                && !source.Process.HaveErrorFlag;
        }

        public IEnumerable<IProcessContainer<TId>> ApplayEnumerable(
            IEnumerable<IProcessContainer<TId>> source,
            object? parameters)
        {
            return source.Where(e => Check(e, parameters));
        }
    }
}
