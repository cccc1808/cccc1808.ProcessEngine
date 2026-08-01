namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.T1
{
    public interface IRedisProcessReservationRunner
    {
        Task RunSubAsync(CancellationToken cancellationToken);

        Task RunTimeoutAsync(CancellationToken cancellationToken);
    }
}