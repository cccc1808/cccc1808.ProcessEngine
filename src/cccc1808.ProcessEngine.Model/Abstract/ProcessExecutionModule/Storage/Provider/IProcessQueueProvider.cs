using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Storage.Provider
{
    public interface IProcessQueueProvider<TId>
    {
        Task<List<MessageDto>> ConsumeRangeAsync(
            int batchSize,
            int uniqueLimit,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken);

        Task<List<MessageDto>> ConsumeSignleAsync(
            int batchSize,
            TimeSpan batchTimeout,
            CancellationToken cancellationToken);

        Task<bool> ProduceAsync(
            ICollection<MessageDto> processes,
            CancellationToken cancellationToken);

        public readonly record struct MessageDto(
            ProcessRegistryDto Registry,
            TId ProcessId);
    }
}