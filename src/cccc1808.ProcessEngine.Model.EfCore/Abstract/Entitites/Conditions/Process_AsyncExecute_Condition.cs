using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Common.Condition;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    /// <summary>
    /// Условие асинхронной обработки процесса.
    /// <see cref="Model.Abstract.Dto.Components.Conditions.IProcessContainer_AsyncExecute_Condition{TId}"/>
    /// </summary>
    public class Process_AsyncExecute_Condition<TId, TProcessEntity>
        :
        IInMemoryCondition<TProcessEntity, DateTimeOffset?>,
        IQueryableCondition<TProcessEntity, DateTimeOffset?>
        where TProcessEntity : ProcessDbEntity<TId>
    {
        public bool Check(
            TProcessEntity source,
            DateTimeOffset? parameters)
        {
            return parameters.HasValue
                ? source.Status == ProcessStatusEnum.AsyncExecute
                && !source.HaveErrorFlag
                && source.TimerDate < parameters.Value
                : source.Status == ProcessStatusEnum.AsyncExecute
                && !source.HaveErrorFlag;
        }

        public IEnumerable<TProcessEntity> ApplayEnumerable(
            IEnumerable<TProcessEntity> source,
            DateTimeOffset? parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<TProcessEntity> ApplayQueryable(
            IQueryable<TProcessEntity> source,
            DateTimeOffset? parameters)
        {
            return parameters.HasValue
                ? source.Where(e =>
                    e.Status == ProcessStatusEnum.AsyncExecute
                    && !e.HaveErrorFlag
                    && e.TimerDate < parameters.Value)
                : source.Where(e =>
                    e.Status == ProcessStatusEnum.AsyncExecute
                    && !e.HaveErrorFlag);
        }
    }
}
