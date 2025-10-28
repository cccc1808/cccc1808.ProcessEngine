using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    /// <summary>
    /// Условие блокировки процесса для выбора обработки.
    /// </summary>
    internal class Process_SelectLock_Condition<TId, TProcessEntity>
        :
        IInMemoryCondition<TProcessEntity, DateTimeOffset>,
        IQueryableCondition<TProcessEntity, DateTimeOffset>
        where TProcessEntity : ProcessDbEntity<TId>
    {
        public bool Check(TProcessEntity source, DateTimeOffset parameters)
        {
            return source.SelectLock < parameters;
        }

        public IEnumerable<TProcessEntity> ApplayEnumerable(IEnumerable<TProcessEntity> source, DateTimeOffset parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<TProcessEntity> ApplayQueryable(IQueryable<TProcessEntity> source, DateTimeOffset parameters)
        {
            return source.Where(e => e.SelectLock < parameters);
        }        
    }
}
