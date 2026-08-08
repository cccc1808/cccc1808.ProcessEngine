namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Паттерны изоляции изменений (атомарности).
    /// </summary>
    public interface IIsolationService
    {
        /// <summary>
        /// Выполнить делегат усползуяю указанную реализацию <see cref="ICompensateService"/> <see cref="IsolationMode"/>.
        /// </summary>
        /// <typeparam name="TParam"></typeparam>
        /// <param name="isolationMode">Тип реализации изоляции <see cref="ICompensateService"/>.</param>
        /// <param name="param">Параметра для измежания замыания.</param>
        /// <param name="action">Действие.</param>
        /// <param name="exceptionHandler">Хендлер ошибки.</param>
        /// <param name="criticalExceptionHandler">Хендлер критической ошибки (должен быть максимально простым и безопасным).</param>
        ValueTask ExecuteAsync<TParam>(
            IsolationMode isolationMode,
            TParam param,
            Func<TParam, CancellationToken, ValueTask> action,
            Func<TParam, Exception, CancellationToken, ValueTask> exceptionHandler,
            Func<TParam, Exception, CancellationToken, ValueTask>? criticalExceptionHandler,
            CancellationToken cancellationToken);

        bool TryGetCurrentScopeInfo(out InScopeInfo scopeInfo);

        /// <summary>
        /// Зарегистрировать действие ручной компенсации.
        /// Действие будет вызвано, если текущий scope будет скомпенсировано.
        /// </summary>
        /// <param name="compensateHandler"></param>
        void RegisterManualCompensate(
            object state,
            Func<int, object, CancellationToken, ValueTask> compensateHandler);

        public enum IsolationMode 
        {
            /// <summary>
            /// Не использовать изоляцию.
            /// (Не рекомендуется, если нужна изоляция).
            /// Пример: используется только, если обработка ошибки идет выше (например пакет из одного элемента),
            ///     или ошибка прерывает транзакцию целиком.
            /// </summary>
            No,

            /// <summary>
            /// Отчистка EF. ChangeTracker.
            /// Ограничение: Для случая, когда внутри в БД ничего не пишется, и не проблема, что отчистятся все данные текущего DIScope.
            /// </summary>
            ClearChangeTracker,

            /// <summary>
            /// Изоляция через DbSavepoint. 
            /// В случае ошибки идет сброс к Savepoint и отчистка <see cref="ClearChangeTracker"/>.
            /// Savepoint предпологает сохранение в БД, поэтому состояние данных до Savepoint данные можно перечитать.
            /// </summary>
            DbSavepointAndClearChangeTracker,

            /// <summary>
            /// Изоляция через ChangeTrackerSnapshot.
            /// Автоматически изолирует только изменения InMemory ChangeTracker.
            /// Ограничение: Писать в БД напрямую запрещено или использовать <see cref="RegisterManualCompensate(Func{CancellationToken, ValueTask})"/>
            /// </summary>
            ChangeTrackerSnapshot,
        }

        public readonly record struct InScopeInfo(int ScopeIndex);
    }
}
