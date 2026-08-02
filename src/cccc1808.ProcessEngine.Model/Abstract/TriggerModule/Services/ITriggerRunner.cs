using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Provider;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services
{
    public interface ITriggerRunner
    {
        /// <summary>
        /// Обработка потока trigger event.
        /// </summary>
        Task ConsumerWorkAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        /// <summary>
        /// Выполнение range триггеров из очереди. 
        /// <see cref="ITriggerQueueProvider{TId}.ConsumeRangeTriggersAsync(int, int, TimeSpan, CancellationToken)"/>
        /// </summary>
        Task RangeTriggerProcessingAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        /// <summary>
        /// Выполнение single триггеров из очереди.
        /// <see cref="ITriggerQueueProvider{TId}.ConsumeSignleTriggersAsync(int, TimeSpan, CancellationToken)(int, int, TimeSpan, CancellationToken)"/>
        /// </summary>
        Task SignleTriggerProcessingAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        /// <summary>
        /// Выборка триггеров из БД в очередь на выполнение.
        /// <see cref="ITriggerQueueProvider{TId}.ProduceTriggersAsync(ICollection{ITriggerQueueProvider{TId}.MessageContainer}, CancellationToken)(int, TimeSpan, CancellationToken)(int, int, TimeSpan, CancellationToken)"/>
        /// </summary>
        Task DbSelectorAsync(
            bool executeOne,
            CancellationToken cancellationToken);
    }
}