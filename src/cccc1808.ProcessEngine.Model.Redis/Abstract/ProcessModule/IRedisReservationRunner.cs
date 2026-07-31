namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule
{
    public interface IRedisReservationRunner
    {
        Task RunSubAsync(CancellationToken cancellationToken);

        Task RunTimeoutAsync(CancellationToken cancellationToken);
    }
}