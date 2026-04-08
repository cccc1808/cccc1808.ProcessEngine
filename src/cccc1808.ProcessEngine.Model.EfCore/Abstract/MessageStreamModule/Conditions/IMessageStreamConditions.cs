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
            IQueryableCondition<TEntity, ForProcessingParamDto1> Query,
            IQueryableCondition<TEntity, ForProcessingParamDto2> QueryIds
            )
            ForProcessing
        {  get; }

        [Obsolete("Сортировка по Priority, OrderId")]
        IQueryableCondition<TProjection, TEntity, ForProcessingParamDto1> ForProcessingProjection<TProjection>(IQueryable<TProjection> _);

        public readonly record struct ForProcessingParamDto1();

        public readonly record struct ForProcessingParamDto2(
            ICollection<TId> ProcessIds
            );
    }
}