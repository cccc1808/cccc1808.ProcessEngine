namespace cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule
{
    public interface ITriggerReservationState<TId>
    {        
        void Reserve(TId processId, DateTimeOffset timeout);

        void Unreserve(TId procesId);

        ISet<TId> GetAll();

        void ClearTimeout(DateTimeOffset date);

        /// <summary>
        /// Отчистка окружения.
        /// </summary>
        void Clear();
    }
}