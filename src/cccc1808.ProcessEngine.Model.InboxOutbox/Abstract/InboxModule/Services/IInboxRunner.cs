namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Services
{
    public interface IInboxRunner
        : IAsyncDisposable
    {
        Task StartAsync(bool oneCycle);

        Task StopAsync();

        Task WaitRunningTasksAsync(
            CancellationToken cancellationToken
            );
    }
}