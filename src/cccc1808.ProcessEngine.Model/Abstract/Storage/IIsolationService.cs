namespace cccc1808.ProcessEngine.Model.Abstract.Storage
{
    /// <summary>
    /// Паттерны изоляции изменений (атомарности).
    /// </summary>
    public interface IIsolationService
    {
        ValueTask ExecuteAsync<TParam>(
            IsolationMode isolationMode,
            TParam param,
            Func<TParam, CancellationToken, ValueTask> action,
            Func<TParam, Exception, CancellationToken, ValueTask> exceptionHandler,
            Func<TParam, Exception, CancellationToken, ValueTask>? criticalExceptionHandler,
            CancellationToken cancellationToken);

        public enum IsolationMode 
        {
            /// <summary>
            /// Не использовать изоляцию.
            /// (Не рекомендуется, если нужна изоляция).
            /// Пример: используется только, если обработка ошибки идет выше (например пакет из одного элемента),
            ///     или ошибка прерывает транзакцию целиком.
            /// </summary>
            [Obsolete]
            No,

            /// <summary>
            /// Отчистка ChangeTracker.
            /// Ограничение: Для случая, когда внутри в БД ничего не пишется, и не проблема, что отчистятся все данные текущего DIScope.
            /// </summary>
            ClearChangeTracker,

            /// <summary>
            /// <see cref="IManualCompensateService"/>
            /// Предпологается, что если в ручной компенсации происходит ошибка,
            ///     то это либо обрабатывается другой вышестоящей изоляцией более универсального типа,
            ///     либо пробрасывается наверх и прерывает транзакцию целиком.
            /// Ограничение: Только ручная компенсация.
            /// Пример: (Делаем Insert в БД, регистрируем - Delete).
            /// </summary>
            Manual,

            /// <summary>
            /// Изоляция через DbSavepoint. 
            /// В случае ошибки идет сброс к Savepoint и отчистка <see cref="ClearChangeTracker"/>.
            /// Savepoint предпологает сохранение в БД, поэтому состояние данных до Savepoint данные можно перечитать.
            /// </summary>
            DbSavepointAndClearChangeTracker,

            /// <summary>
            /// Изоляция через ChangeTrackerSnapshot.
            /// Автоматически изолирует только изменения InMemory ChangeTracker.
            /// Ограничение: Писать в БД напрямую запрещено.
            /// </summary>
            ChangeTrackerSnapshot,

            /// <summary>
            /// <see cref="ChangeTrackerSnapshot"/>  и <see cref="Manual"/>.
            /// </summary>
            ChangeTrackerSnapshotAndManual
        }
    }
}
