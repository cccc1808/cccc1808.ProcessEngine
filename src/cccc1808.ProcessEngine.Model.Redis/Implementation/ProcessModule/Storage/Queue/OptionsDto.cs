using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.ProcessModule.Storage.Queue
{
    public class OptionsDto<TId>
    {
        public required string ConnectionName { get; set; }

        public required int DbId { get; set; }

        public required Func<ProcessRegistryDto, string> QueueChannelNameFactory { get; set; }

        /// <summary>
        /// Ограничение размера очереди по <see cref="ProcessRegistryDto"/>.
        /// TODO: можно сделать функцией от типа процесса, чтобы можно было сделать разные значения.
        /// </summary>
        public int QueueSizeLimit { get; set; }
            = 10000;

        /// <summary>
        /// Количетсво SortedSet, оправшиваемых за один запрос в Redis.
        /// (Если мы узнаем что очередь пустая, то она вытесняется до тех пор пока по ней не прилет оповещение о поступлении сообщения,
        /// и обработчик движется к другим непустым очередям).
        /// </summary>
        public int SearchSetsPerQueryLimit { get; set; }
            = 30;

        public required Func<ProcessRegistryDto, string> ProcessToQueueSetNameFactory { get; set; }

        public required Func<string, ProcessRegistryDto> QueueSetNameToProcessTypeFactory { get; set; }

        public required Func<TId, string> IdToString { get; set; }

        public required Func<string, TId> StringToId { get; set; }
    }
}
