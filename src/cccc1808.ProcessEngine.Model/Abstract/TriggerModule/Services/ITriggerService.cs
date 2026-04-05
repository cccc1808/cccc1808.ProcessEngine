namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    internal interface ITriggerService
    {
        Task ConsumerWorkAsync(CancellationToken cancellationToken);

        Task DbWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken);
    }
}