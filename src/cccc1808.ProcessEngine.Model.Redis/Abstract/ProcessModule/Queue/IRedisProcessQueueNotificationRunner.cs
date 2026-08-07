using cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Queue;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue
{
    /// <summary>
    /// Раннер, который отслеживает оповещения о поступлении в очередь новых сообщений.
    /// <see cref="RedisProcessQueueProvider{TId}"/>, <see cref="RedisNotifyProcessQueueState"/>.
    /// </summary>
    public interface IRedisProcessQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}