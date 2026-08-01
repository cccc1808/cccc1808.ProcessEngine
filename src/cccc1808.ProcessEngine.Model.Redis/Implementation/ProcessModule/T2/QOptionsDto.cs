using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.T2
{
    public class QOptionsDto<TId>
    {
        public required string ConnectionName { get; set; }

        public required int DbId { get; set; }

        public required Func<ProcessRegistryDto, string> QueueChannelNameFactory { get; set; }

        public int QueueSizeLimit { get; set; }
            = 10000;

        public int SearchSetsPerQueryLimit { get; set; }
            = 30;

        public required Func<ProcessRegistryDto, string> ProcessToQueueSetNameFactory { get; set; }

        public required Func<string, ProcessRegistryDto> QueueSetNameToProcessTypeFactory { get; set; }

        public required Func<TId, string> IdToString { get; set; }

        public required Func<string, TId> StringToId { get; set; }
    }
}
