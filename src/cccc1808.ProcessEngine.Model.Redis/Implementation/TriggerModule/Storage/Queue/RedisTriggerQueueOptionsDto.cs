using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Dto;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.Storage.Queue
{
    public class RedisTriggerQueueOptionsDto<TId>
    {
        public required string ConnectionName { get; set; }

        public required int DbId { get; set; }

        public required Func<TriggerTypeUniqueDto, string> QueueChannelNameFactory { get; set; }

        /// <summary>
        /// Ограничение размера очереди по HandlerKey.
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

        public required Func<TriggerTypeUniqueDto, string> HandlerToQueueSetNameFactory { get; set; }

        public required Func<string, TriggerTypeUniqueDto> QueueSetNameToHandlerFactory { get; set; }

        public required Func<TId, string> IdToString { get; set; }

        public required Func<string, TId> StringToId { get; set; }
    }
}
