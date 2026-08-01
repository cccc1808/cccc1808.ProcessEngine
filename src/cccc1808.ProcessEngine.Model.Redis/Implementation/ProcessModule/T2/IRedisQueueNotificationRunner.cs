
namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.T2
{
    public interface IRedisQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}