namespace cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2
{
    public interface IRedisTriggerQueueNotificationRunner
    {
        Task RunAsync(
            bool one,
            CancellationToken cancellationToken);
    }
}