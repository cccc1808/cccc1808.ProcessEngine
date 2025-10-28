using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites.Conditions
{
    /// <summary>
    /// Условие асинхронной обработки процесса.
    /// <see cref="Model.Abstract.Dto.Components.Conditions.IProcessContainer_AsyncExecute_Condition{TId}"/>
    /// </summary>
    internal class Process_AsyncExecute_Condition<TId, TProcessEntity>
        : 
        IInMemoryCondition<TProcessEntity, object?>,
        IQueryableCondition<TProcessEntity, object?>
        where TProcessEntity : ProcessDbEntity<TId>
    {
        public bool Check(
            TProcessEntity source,
            object? parameters)
        {
            return
                source.Status == ProcessStatusEnum.AsyncExecute
                && !source.HaveErrorFlag;
        }

        public IEnumerable<TProcessEntity> ApplayEnumerable(
            IEnumerable<TProcessEntity> source,
            object? parameters)
        {
            return source.Where(e => Check(e, parameters));
        }

        public IQueryable<TProcessEntity> ApplayQueryable(
            IQueryable<TProcessEntity> source,
            object? parameters)
        {
            return source.Where(e => 
                e.Status == ProcessStatusEnum.AsyncExecute 
                && !e.HaveErrorFlag);
        }        
    }
}
