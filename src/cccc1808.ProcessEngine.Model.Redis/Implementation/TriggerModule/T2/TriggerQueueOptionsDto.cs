using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Redis.Abstract.TriggerModule.T2;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule.T2
{
    public class TriggerQueueOptionsDto<TId>
    {
        public required string ConnectionName { get; set; }

        public required int DbId { get; set; }

        public required Func<IRedisNotifyTriggerQueueState.KeyDto, string> QueueChannelNameFactory { get; set; }

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

        public required Func<IRedisNotifyTriggerQueueState.KeyDto, string> HandlerToQueueSetNameFactory { get; set; }

        public required Func<string, IRedisNotifyTriggerQueueState.KeyDto> QueueSetNameToHandlerFactory { get; set; }

        public required Func<TId, string> IdToString { get; set; }

        public required Func<string, TId> StringToId { get; set; }
    }
}
