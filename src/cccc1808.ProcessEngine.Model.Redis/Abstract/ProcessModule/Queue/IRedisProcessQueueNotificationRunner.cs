namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.Queue
{
    public interface IRedisProcessQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}