using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query;
using cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Services
{
    public class WakeupService<TId> 
        : IWakeupService<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IProcessSetter _processSetter;
        private readonly IWakeupRegistry<TId> _wakeupRegistry;
        private readonly IWakeupServiceQueries<TId> _wakeupServiceQueries;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;

        private readonly OptionsDto _optionsDto;

        public WakeupService(
            IServiceProvider serviceProvider,
            IProcessSetter processSetter,
            IWakeupRegistry<TId> wakeupRegistry,
            IWakeupServiceQueries<TId> wakeupServiceQueries,

            IProcessContainerConditions<TId> processContainerConditions,

            OptionsDto optionsDto)
        {
            _serviceProvider = serviceProvider;
            _processSetter = processSetter;
            _wakeupRegistry = wakeupRegistry;
            _wakeupServiceQueries = wakeupServiceQueries;

            _processContainerConditions = processContainerConditions;

            _optionsDto = optionsDto;
        }

        #region IWakeUpService

        public async Task<ICollection<IProcessContainer<TId>>> AfterAsyncSessionHandlerAsync(
            ICollection<IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            static Dictionary<TId, ExecuteContextItemDto> BuildContext(
                WakeupService<TId> This,
                ICollection<IProcessContainer<TId>> processes)
            {
                var context = new Dictionary<TId, ExecuteContextItemDto>(processes.Count);

                foreach (var elem in processes)
                {
                    if (!elem.UsingWakeup)
                    {
                        continue;
                    }

                    if (!elem.InAsyncExecuting)
                    {
                        throw new InvalidOperationException("[Bug] Данный метод вызывается только в конце асинхронной обработки.");
                    }

                    // Игнорируем процессы с ошибкой.
                    if (This._processContainerConditions.HaveError.Check(elem))
                    {
                        continue;
                    }

                    // Обрабатываем только указанные статусы.
                    if (elem.Process.Status is not ProcessStatusEnum.WaitEvent)
                    {
                        // Info:
                        // * AsyncExecuting - ничего обновлять и проверять не нужно (AsyncExecuting -> AsycnExecuting),
                        // * Complete - не обрабатывается
                        continue;
                    }

                    context.Add(
                        elem.Id,
                        new ExecuteContextItemDto(elem));
                }

                return context;
            }

            /// <summary>
            /// Блокировка используется, чтобы не допустить ситуации, когда другая транзакция попытается пробудить процесс,
            /// а мы это не увидим (и процесс уснет)
            /// (ждем завершения блокировок всех сигналов).
            /// </summary>
            static async Task LockWakeupStateAndCheckCondition(
                WakeupService<TId> This,
                Dictionary<TId, ExecuteContextItemDto> context,
                CancellationToken cancellationToken)
            {
                // 1) Получаем wakeup с блокировкой
                // (это небходимо, чтобы если сейчас идет попытка пробуждения по новому внешнему сигналу, то она будет выполняться после завершения это транзакции, и новый сигнал не потеряется).
                var dbData = await This._wakeupServiceQueries.AfterSession_LoadStateWithLockAsync(
                    context.Keys,
                    cancellationToken);

                foreach (var elem in context.Values)
                {
                    var dbDataElem = dbData[elem.Process.Id];

                    elem.WakeupId = dbDataElem.Id;
                    elem.WakeupWithLockIsAsyncExecuting = dbDataElem.IsAsyncExecuting;
                }
                
                // 2) Блокировка получена, вызываем кастомную логику проверки условия: 
                var checkGroups = context.Values
                    .Select(e => (
                        Element: e,
                        Handler: This._wakeupRegistry.GetCheckHandler(This._serviceProvider, e.Process.Process.Info.ProcessType)))
                    .GroupBy(e => e.Handler);

                foreach (var elem in checkGroups)
                {
                    var processes = elem.Select(e => e.Element.Process).ToArray();

                    var result = await elem.Key.HandleRangeAsync(
                        processes,
                        cancellationToken);

                    foreach (var elem2 in elem)
                    {
                        elem2.Element.WakeupCheckResult = result[elem2.Element.Process.Id];
                    }
                }
            }

            /// <summary>
            /// Обработка результатов, выставления статуса пробуждения.
            /// </summary>
            static List<IProcessContainer<TId>> ExecuteWakeup(
                WakeupService<TId> This,
                Dictionary<TId, ExecuteContextItemDto> context)
            {
                var forUpdate = new List<IProcessContainer<TId>>(context.Count);

                foreach (var elem in context.Values)
                {
                    var needUpdate = false;
                    var isAsyncExecuting = false;

                    if (elem.WakeupCheckResult)
                    {
                        needUpdate =
                            !elem.WakeupWithLockIsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.AsyncExecute;
                        isAsyncExecuting = true;
                    }
                    else
                    {
                        needUpdate =
                            elem.WakeupWithLockIsAsyncExecuting
                            || elem.Process.Process.Status != ProcessStatusEnum.WaitEvent;
                        isAsyncExecuting = false;
                    }

                    if (needUpdate)
                    {
                        This._processSetter.SetStatus(
                            elem.Process,
                            isAsyncExecuting ? ProcessStatusEnum.AsyncExecute : ProcessStatusEnum.WaitEvent);

                        elem.Process.AddComponent<IWakeupComponent<TId>>(
                            new WakeupComponent<TId>(
                                elem.WakeupId, 
                                isAsyncExecuting,
                                needUpdate: true)
                            );

                        forUpdate.Add(elem.Process);
                    }
                }

                return forUpdate;
            }

            var context = BuildContext(this, processes);
            if (context.Count == 0)
            {
                return Array.Empty<IProcessContainer<TId>>();
            }

            await LockWakeupStateAndCheckCondition(this, context, cancellationToken);

            var forUpdate = ExecuteWakeup(this, context);
            return forUpdate;
        }

        public async Task WakeupProcessHandlerAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken)
        {
            if (ids.Count == 0)
            {
                return;
            }

            var wakeupContext = await _wakeupServiceQueries.Wakeup_LoadStateAsync(
                ids, 
                _optionsDto.WakeupTryUpdatelockTimeout,
                cancellationToken);

            // Процессы не в состоянии обработки т.к. мы получили updatelock на wakeup и увидели статус WaitEvent.
            await _wakeupServiceQueries.Wakeup_LoadProcessesWithLockAsync(
                wakeupContext,
                cancellationToken);

            // 3) Обновляем wakeup и process
            foreach (var elem in wakeupContext.Data.Values)
            {
                var processState = elem.ProcessState 
                    ?? throw new InvalidOperationException($"[Bug]. Ожидается наличие {nameof(elem.ProcessState)}.");

                if (processState.StoppedByError || processState.RetryCount.HasValue) // TODO: condition
                {
                    // Если процесс в ошибке, то не трогаем его.
                    continue;
                }

                if (processState.Status == ProcessStatusEnum.Complete) // TODO: condition
                {
                    // Если процес завершился, то не трогам.
                    continue;
                }

                wakeupContext.ToWakeupData.Add(elem);
            }

            await _wakeupServiceQueries.Wakeup_ExecuteAsync(
                wakeupContext, 
                cancellationToken);
        }

        #endregion


        private class ExecuteContextItemDto
        {
            public IProcessContainer<TId> Process { get; }

            public TId WakeupId { get; set; }

            public bool WakeupWithLockIsAsyncExecuting { get; set; }

            public bool WakeupCheckResult { get; set; }


            public ExecuteContextItemDto(IProcessContainer<TId> process)
            {
                Process = process;

                WakeupId = default!;
                WakeupWithLockIsAsyncExecuting = false;
                WakeupCheckResult = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="WakeupTryUpdatelockTimeout">Timeout попытки получения updlock на wakeup.</param>
        public record OptionsDto(
            TimeSpan WakeupTryUpdatelockTimeout
            )
        {
            public OptionsDto(
                TimeSpan? WakeupEndUpdLockTimeout = null
                )
                : this(
                      WakeupEndUpdLockTimeout ?? TimeSpan.FromSeconds(2)
                      )
            {
            }
        }
    }
}
