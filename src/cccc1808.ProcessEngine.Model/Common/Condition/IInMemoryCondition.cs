using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Common.Condition
{
    public interface IInMemoryCondition<TData, TParameters>
    {
        /// <summary>
        /// Проверить условие.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        bool Check(TData source, TParameters parameters);

        /// <summary>
        /// Фильтрация для InMemory.
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        IEnumerable<TData> ApplayEnumerable(IEnumerable<TData> source, TParameters parameters);
    }
}
