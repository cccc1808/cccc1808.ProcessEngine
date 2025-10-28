using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions
{
    /// <summary>
    /// Условие асинхронной обработки процесса.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class IProcessContainer_AsyncExecute_Condition<TId>
        : IInMemoryCondition<IProcessContainer<TId>, DateTimeOffset>
    {
        public bool Check(
            IProcessContainer<TId> source,
            DateTimeOffset parameters)
        {
            return 
                source.Process.Status == ProcessStatusEnum.AsyncExecute
                && !source.Process.HaveErrorFlag
                && source.Process.TimerDate < parameters;
        }

        public IEnumerable<IProcessContainer<TId>> ApplayEnumerable(
            IEnumerable<IProcessContainer<TId>> source,
            DateTimeOffset parameters)
        {
            return source.Where(e => Check(e, parameters));
        }
    }
}
