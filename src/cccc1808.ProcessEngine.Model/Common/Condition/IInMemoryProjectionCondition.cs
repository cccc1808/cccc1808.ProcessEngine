using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Common.Condition
{
    public interface IInMemoryProjectionCondition<TData, TTarget>
    {
        TTarget ApplayProjection(TData source);

        /// <summary>
        /// Проекция для InMemory.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        IEnumerable<TTarget> ApplayProjectionEnumerable(IEnumerable<TData> source);
    }
}
