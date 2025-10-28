namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.QueueProvider
{
    public interface IQueueProviderFactory
    {
        ValueTask<IQueueProducer> GetProducerAsync(
            string name, 
            CancellationToken cancellationToken);

        ValueTask<IQueueConsumer> GetConsumerAsync(
            string name,
            CancellationToken cancellationToken);
    }
}
