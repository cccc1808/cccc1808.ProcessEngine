namespace cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule
{
    public interface ITriggerRedisReservationRunner
    {
        Task RunSubAsync(CancellationToken cancellationToken);

        Task RunTimeoutAsync(CancellationToken cancellationToken);
    }
}