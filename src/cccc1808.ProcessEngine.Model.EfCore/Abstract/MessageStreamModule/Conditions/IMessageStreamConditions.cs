using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions
{
    public interface IMessageStreamConditions<TId, TEntity>
        where TEntity : IMessageDbEntity<TId>
    {
        /// <summary>
        /// Признак наличия активных сообщений.
        /// </summary>
        (
            object? _, 
            IQueryableCondition<TEntity> Query)
            IsActiveMessages { get; }

        /// <summary>
        /// Отбор сообщений для обработки на основании идентификаторов стримов.
        /// </summary>
        (
            object? _, 
            IQueryableCondition<TEntity, ForProcessingParamDto> Query
            )
            ForProcessing
        {  get; }

        public readonly record struct ForProcessingParamDto(
            ICollection<TId> ProcessIds,
            bool WithPriorityOrdering
            );
    }
}