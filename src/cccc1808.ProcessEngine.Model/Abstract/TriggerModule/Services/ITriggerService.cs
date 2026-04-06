namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    public interface ITriggerService
    {
        Task ConsumerWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        Task DbWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken);
    }
}