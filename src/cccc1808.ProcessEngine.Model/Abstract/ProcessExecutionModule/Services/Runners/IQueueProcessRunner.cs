namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners
{
    public interface IQueueProcessRunner
    {
        /// <summary>
        /// Запустить раннер, который считывает процессы для обработки из БД и помещает в очередь.
        /// </summary>
        Task DbSelectExecuteAsync(bool executeOne, CancellationToken cancellationToken);

        /// <summary>
        /// Запустить раннер, который считывает из очереди и обрабатывает процессы.
        /// </summary>
        Task RunRangeExecuteAsync(bool executeOne, CancellationToken cancellationToken);

        /// <summary>
        /// Запустить раннер, который считывает из очереди и обрабатывает процессы.
        /// </summary>
        Task RunSingleExecuteAsync(bool executeOne, CancellationToken cancellationToken);
    }
}