using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    public interface ITriggerEventOutboxRunner<TId>
    {
        Task RunAsync(
            bool oneCycle,
            CancellationToken cancellationToken);
    }
}