namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.T2
{
    public interface IRedisProcessQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}