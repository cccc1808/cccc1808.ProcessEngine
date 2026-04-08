namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    public interface ITriggerRunner
    {
        Task ConsumerWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        Task DbWorkAsync(
            bool oneCycle,
            CancellationToken cancellationToken);
    }
}