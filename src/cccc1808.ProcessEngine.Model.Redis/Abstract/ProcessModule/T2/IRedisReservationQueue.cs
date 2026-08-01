using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Abstract.ProcessModule.T2
{
    public interface IRedisReservationQueue<TId>
    {
        Task<List<MessageDto>> ConsumeAsync(
            int batchSize,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken);

        Task<ICollection<MessageDto>> ProduceAsync(
            ICollection<MessageDto> processes,
            CancellationToken cancellationToken);

        public readonly record struct MessageDto(
            ProcessRegistryDto Registry,
            TId ProcessId);
    }
}