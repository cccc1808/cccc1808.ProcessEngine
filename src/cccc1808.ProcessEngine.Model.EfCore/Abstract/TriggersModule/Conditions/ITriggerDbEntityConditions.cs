using cccc1808.ProcessEngine.Model.Abstract.ConditionModule;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Conditions
{
    public interface ITriggerDbEntityConditions<TId>
    {
        (
            object? _,
            IQueryableCondition<TriggerDbEntity<TId>, ICollection<string>> QueryRange
            )
            Key
        { get; }

        (
            object? _,
            IQueryableCondition<TriggerDbEntity<TId>, ICollection<string>> QueryRange
            )
            KeyAndNotComplete
        { get; }

        (
            object? _, 
            IQueryableCondition<TriggerDbEntity<TId>, DbProcessingForSelectorParameters> Query
            ) 
            DbProcessingForSelector { get; }

        (
            object? _,
            IQueryableCondition<TriggerDbEntity<TId>, DbProcessingForHandlerParameters> Query
            )
            DbProcessingForHandler
        { get; }


        public readonly record struct DbProcessingForSelectorParameters(
            DateTimeOffset NowDate);

        public readonly record struct DbProcessingForHandlerParameters(
            DateTimeOffset NowDate,
            ICollection<TId> ids);
    }
}