using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers
{
    /// <summary>
    /// Хендлер для дочерних триггеров при использовании <see cref="ITriggerComponent.TriggerKind.SimpleStreamRoot"/>.
    /// Передает сигнал не на процесс, а на root триггер.
    /// 
    /// https://wiki.denhome.ru/bin/view/Проекты%20и%20репозитории/Библиотеки/Движок%20cccc1808.%20ProcessEngine/Про%20передачу%20сигнала%20на%20процесс/
    /// Типы передачи сигналов 2.2.1
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class BaseRootChildTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        private readonly ITriggerEventRaiser<TId> _eventRaiser;

        public BaseRootChildTriggerRangeHandler(
            ITriggerEventRaiser<TId> eventRaiser)
        {
            _eventRaiser = eventRaiser;
        }

        public virtual async ValueTask<IDictionary<string, ITriggerHandler.Result>> HandleAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            CancellationToken cancellationToken)
        {
            var checkResult = await CheckAsync(triggers, cancellationToken);

            var toRootTriggerEvents = checkResult.Values
                .Where(e => e.NeedSignal)
                .Select(
                    e => new ITriggerEventRaiser<TId>.RaiseContainer(
                            e.RootTroggerQueueName,
                            e.ProcessId,
                            new SignalSimpleStreamTriggerEvent(e.RootTriggerKey)
                            )
                )
                .ToArray();

            await _eventRaiser.RaiseAsync(
                toRootTriggerEvents,
                cancellationToken);

            return checkResult.ToDictionary(e => e.Key, e => e.Value.Result);
        }

        /// <summary>
        /// Проверить условие о необходимости срабатывания триггера.
        /// Указать идентефикатор корневого процесса и очереди.
        /// </summary>
        /// <param name="triggers"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected abstract ValueTask<IDictionary<string, ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Result"></param>
        /// <param name="ProcessId">Id процесса.</param>
        /// <param name="NeedSignal">Нужно ли передавать сигнал на root триггер (есть ли необработанный сигнал на триггере).</param>
        /// <param name="RootTriggerKey">Ключ root триггера.</param>
        /// <param name="RootTroggerQueueName">Имя очередь для публикации <see cref="ITriggerEvent"/>.</param>
        public record ResultDto(
            ITriggerHandler.Result Result,
            TId ProcessId,
            bool NeedSignal,
            string RootTriggerKey,
            string RootTroggerQueueName
            );
    }
}
