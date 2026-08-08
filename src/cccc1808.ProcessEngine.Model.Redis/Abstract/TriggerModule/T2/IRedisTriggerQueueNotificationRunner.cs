using cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.T2;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2
{
    /// <summary>
    /// Раннер, который отслеживает оповещения о поступлении в очередь новых сообщений.
    /// <see cref="RedisTriggerQueueProvider{TId}"/>, <see cref="IRedisNotifyTriggerQueueState"/>.
    /// </summary>
    public interface IRedisTriggerQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}