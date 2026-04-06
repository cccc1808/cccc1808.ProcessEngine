namespace cccc1808.ProcessEngine.Model.Abstract.QueueModule.Provider
{
    /// <summary>
    /// Фабрика для получения экзепляров consumer и producer.
    /// </summary>
    public interface IQueueProviderFactory
    {
        ValueTask<IQueueProducer> GetProducerAsync(
            string name, 
            CancellationToken cancellationToken);

        ValueTask<IQueueConsumer> GetConsumerAsync(
            string name,
            CancellationToken cancellationToken);

        ValueTask<bool> DisconnectConsumerAsync(
            string name,
            CancellationToken cancellationToken);
    }
}
