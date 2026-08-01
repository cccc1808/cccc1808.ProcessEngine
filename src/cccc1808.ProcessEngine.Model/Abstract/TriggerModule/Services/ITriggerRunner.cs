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
        /// </summary>
        Task RangeTriggerProcessingAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        /// <summary>
        /// Выполнение single триггеров из очереди>
        /// </summary>
        Task SignleTriggerProcessingAsync(
            bool executeOne,
            CancellationToken cancellationToken);

        /// <summary>
        /// Выбора триггеров из БД в очередь на выполнение.
        /// </summary>
        Task DbSelectorAsync(
            bool executeOne,
            CancellationToken cancellationToken);
    }
}