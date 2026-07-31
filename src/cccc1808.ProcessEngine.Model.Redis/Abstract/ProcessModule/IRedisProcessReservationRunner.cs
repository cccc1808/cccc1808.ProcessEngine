namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule
{
    public interface IRedisProcessReservationRunner
    {
        Task RunSubAsync(CancellationToken cancellationToken);

        Task RunTimeoutAsync(CancellationToken cancellationToken);
    }
}