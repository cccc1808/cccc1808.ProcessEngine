using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Handlers.Retry
{
    public class NoWakeupRetryTriggerRangeHandler<TId>
        : ITriggerRangeHandler<TId>
    {
        public const string Name = "NoWakeupRetryTriggerRangeHandler";

        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ITriggerHandlerFacade<TId> _triggerHandlerFacade;

        public NoWakeupRetryTriggerRangeHandler(
            IDateTimeProvider dateTimeProvider, 
            ITriggerHandlerFacade<TId> triggerHandlerFacade)
        {
            _dateTimeProvider = dateTimeProvider;
            _triggerHandlerFacade = triggerHandlerFacade;
        }

        public async ValueTask<IDictionary<string, ITriggerRangeHandler<TId>.ResultDto>> CheckAsync(
            IEnumerable<ITriggerComponent<TId>> triggers, 
            bool isEmergencyTrigger, 
            CancellationToken cancellationToken)
        {
            var processFounded = await _triggerHandlerFacade.LockForWaitProcessAsync(
                triggers,
                cancellationToken);

            var result = new Dictionary<string, ITriggerRangeHandler<TId>.ResultDto>(triggers.Count());

            // Процесс в ожидании и блокировка получена.
            foreach (var elem in processFounded.WaitWithLock)
            {
                result.Add(
                    elem.Key, 
                    new ITriggerRangeHandler<TId>.ResultDto(
                        ITriggerHandler.ResultDto.RemoveResult(),
                        NeedExecute: true));
            }

            // Процесс в ожидании, но не смогли получить блокировку на него.
            foreach (var elem in processFounded.WaitWithoutLock)
            {
                result.Add(
                    elem.Key,
                    new ITriggerRangeHandler<TId>.ResultDto(
                        ITriggerHandler.ResultDto.ActivateResult(_dateTimeProvider.UtcNow + TimeSpan.FromSeconds(5)),
                        NeedExecute: false));
            }

            // Процесс и так активен, ничего не делаем.
            foreach (var elem in processFounded.IsAsyncExecuting)
            {
                result.Add(
                    elem.Key,
                    new ITriggerRangeHandler<TId>.ResultDto(
                        ITriggerHandler.ResultDto.RemoveResult(),
                        NeedExecute: false));
            }

            // Процесс завершен.
            foreach (var elem in processFounded.InComplete)
            {
                result.Add(
                    elem.Key,
                    new ITriggerRangeHandler<TId>.ResultDto(
                        ITriggerHandler.ResultDto.RemoveResult(),
                        NeedExecute: false));
            }

            // Процесс не найден.
            foreach (var elem in processFounded.InComplete)
            {
                result.Add(
                    elem.Key,
                    new ITriggerRangeHandler<TId>.ResultDto(
                        ITriggerHandler.ResultDto.RemoveResult(),
                        NeedExecute: false));
            }

            return result;
        }        

        public async ValueTask ExecuteAsync(
            IEnumerable<ITriggerComponent<TId>> triggers,
            CancellationToken cancellationToken)
        {
            await _triggerHandlerFacade.ToAsyncExecutingNoWakeupAsync(
                triggers.Select(e => e.ProcessId).ToArray(), 
                cancellationToken);
        }
    }
}
