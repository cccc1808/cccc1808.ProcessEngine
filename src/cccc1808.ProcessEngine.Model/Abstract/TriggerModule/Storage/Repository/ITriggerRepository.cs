using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository
{
    public interface ITriggerRepository<TId>
    {
        Task<IDictionary<string, ITriggerComponent<TId>>> LoadTriggerForQueueConsumerAsync(
            ICollection<string> keys,
            CancellationToken cancellationToken);

        /// <summary>
        /// Попытаться загрузить триггеры для обработки.
        /// Пытается получить update lock, возвращает только те записи, где он был получен за указанный waitLockTimeout.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="waitLockTimeout">Время ожидания update lock.</param>
        /// <returns></returns>
        Task<ICollection<ITriggerComponent<TId>>> LoadForHandlerAsync(
            ICollection<TId> ids, 
            TimeSpan waitLockTimeout,
            CancellationToken cancellationToken);

        Task CreateTriggerAsync(
            CreateTriggerDto createDto,
            CancellationToken cancellationToken);

        Task CreateTriggerRangeAsync(
            ICollection<CreateTriggerDto> createDto,
            CancellationToken cancellationToken);

        Task SaveAsync(
            ICollection<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken);
        
        public readonly record struct CreateTriggerDto(
            string key,
            DateTimeOffset timerDate,
            TId processId,
            string handlerKey,
            ITriggerComponent<TId>.TriggerKind kind,
            short priority,
            bool isActivated,
            int? counter,
            (
                DefaultTriggerComponent.SimpleStreamDto<TId>? simpleStream,
                DefaultTriggerComponent.OffsetStreamDto<TId>? offsettampStream)? streamState);

        public class StreamDto
        {
            public bool StreamsProcessIsWaiting { get; set; }

            public Dictionary<string, long> StreamsTimeStamp { get; set; } = default!;

            public Dictionary<string, long> StreamProcessTimestamps { get; set; } = default!;
        }
    }
}
