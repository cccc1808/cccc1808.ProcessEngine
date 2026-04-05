namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Система компенсации с поддержкой дествий с указанием ручной компенсации.
    /// </summary>
    public interface IManualCompensateService
        : ICompensateService
    {
        /// <summary>
        /// Сохранить хендлер компенсации (после выполнения действия).
        /// </summary>
        void AddCompensate(Func<CancellationToken, ValueTask> compensate);

        /// <summary>
        /// Выполнить действие и после сохранить хендлер компенсации.
        /// </summary>
        ValueTask ExecuteWithCompensate(
            Func<ValueTask> action,
            Func<CancellationToken, ValueTask> compensate);
    }
}
