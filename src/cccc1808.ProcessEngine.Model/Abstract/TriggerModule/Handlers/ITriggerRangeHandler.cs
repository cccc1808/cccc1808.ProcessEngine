using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers
{
    public interface ITriggerRangeHandler<TId> 
        : ITriggerHandler
    {
        ValueTask<IDictionary<string, Result>> HandleAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);
    }
}
