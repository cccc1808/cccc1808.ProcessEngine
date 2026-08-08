using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Queue;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.Queue
{
    /// <summary>
    /// Раннер, который отслеживает оповещения о поступлении в очередь новых сообщений.
    /// <see cref="RedisTriggerQueueProvider{TId}"/>, <see cref="IRedisTriggerQueueNotifyState"/>.
    /// </summary>
    public interface IRedisTriggerQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}