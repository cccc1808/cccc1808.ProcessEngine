using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers
{
    public interface ITriggerRangeHandler<TId> 
        : ITriggerHandler
    {
        /// <summary>
        /// Выполнить хендлер для рабора триггеров.
        /// </summary>
        /// <param name="triggers">Триггеры.</param>
        /// <param name="isEmergencyTrigger">Вызов их раннера или из страхующего триггера.</param>
        /// <returns>Обязательный результат хендлера.</returns>
        ValueTask<IDictionary<string, ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            bool isEmergencyTrigger,
            CancellationToken cancellationToken);

        public ValueTask<ISet<TId>> ExecuteAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);

        public new readonly record struct ResultDto(
            ITriggerHandler.ResultDto Result,
            bool NeedExecute
            );
    }
}
