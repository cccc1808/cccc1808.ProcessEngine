using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute
{
    /// <summary>
    /// Позволяет использовать разные типы обработки, например:
    /// * Обрабатывать по одному без сохранения в БД и в конце общий SaveChanges.
    /// * Обрабатывать батчами с сохранением после каждого шага.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    /// <typeparam name="TContext"></typeparam>
    public class ExecuteStepByStepGroupMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IIsolationService _isolationService;
        private readonly IProcessSetter _processSetter;
        private readonly IWakeupService<TId> _wakeupService;        

        private readonly Func<IServiceProvider, ValueTask<IHandler>> _factory;

        private readonly IProcessContainerConditions<TId> _processContainerConditions;

        public ExecuteStepByStepGroupMiddleware(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            IIsolationService isolationService,
            IProcessSetter processSetter,
            IWakeupService<TId> wakeupService,

            Func<IServiceProvider, ValueTask<IHandler>> factory,

            IProcessContainerConditions<TId> processContainerConditions)
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
            _isolationService = isolationService;
            _processSetter = processSetter;
            _wakeupService = wakeupService;

            _factory = factory;

            _processContainerConditions = processContainerConditions;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
            static bool StopCheck(
                ExecuteStepByStepGroupMiddleware<TId> This,
                IProcessContainer<TId> elem
                ) 
            {
                // 3.1) Условие асинхронной обработки процесса.
                var stopInCurrentSession = elem.CurrentSession.StopAsyncProcessingSession
                    || !This._processContainerConditions.AsyncExecute.Memory.Check(elem);

                if (stopInCurrentSession)
                {                                     
                    return true;
                }

                return false;
            }

            //// 1) 
            if (ids.Count != 1)
            {
                throw new ArgumentException();
            }

            var sessionId = Guid.NewGuid();

            var handler = await _factory(_serviceProvider);
            var options = handler.Options;

            var allProcesses = LinkContainer.Create(
                await LoadAsync(
                    handler,
                    ids.First(),
                    sessionId,
                    cancellationToken)
                );

            if (allProcesses.Data.Count == 0)
            {
                return;
            }

            var softTimeoutDate = DateTimeOffset.MaxValue;

            //// 2) Выполняемые процессы
            var executingProcesses = LinkContainer.Create(
                allProcesses
                    .Data
                    .Where(
                        e => 
                        {
                            if (e.Value.TryGetComponent<ISoftTimeoutComponent>(out var softTimeoutComponent))
                            {
                                softTimeoutDate = DateTimeOffsetHelper.Min(
                                    softTimeoutDate,
                                    softTimeoutComponent.StopDate ?? DateTimeOffset.MaxValue);
                            }

                            return !StopCheck(this, e.Value);
                        })
                    .ToDictionary(
                        e => e.Key,
                        e => e.Value
                        )
                );

            while (true)
            {
                // Весь набор обработан.
                if (!executingProcesses.Data.Any())
                {
                    break;
                }

                // Soft timeout.
                if (_dateTimeProvider.UtcNow > softTimeoutDate)
                {
                    break;
                }

                var executionGroup = new LinkContainer<ExecuteGroup?>(null);
                await _isolationService.ExecuteAsync(
                    options.IsolationMode,
                    (
                        This: this,
                        handler,
                        options,
                        sessionId,

                        allProcesses,
                        executingProcesses,
                        executionGroup                      
                        ),
                    static async (p, cancellationToken) =>
                    {
                        // 1) Формирование группы выполнения.
                        p.executionGroup.Data = await p.handler.GetExecutionGroupAsync(
                            p.executingProcesses.Data,
                            cancellationToken);

                        // 2) Шаг.
                        await p.handler.StepRangeAsync(
                            p.executionGroup.Data.Value,
                            cancellationToken);

                        // 3) Проверка завершенных процессов
                        var forStop = new List<TId>();
                        {
                            // Условие остановки процесса.
                            foreach (var elem in p.executionGroup.Data.Value.Group.Values)
                            {
                                // 3.1) Условие асинхронной обработки процесса.
                                if (StopCheck(p.This, elem))
                                {
                                    ////  Обработка завершена  
                                    if (!elem.CurrentSession.CurrentSessionHaveError && elem.CurrentSession.ClearErrorOnSessionEnd )
                                    {
                                        // В сессии нет ошибок, тогда отчищаем ошибку.
                                        // TODO: подумать нужно ли сбрасывать ошибку если это elem.CurrentSession.StopAsyncProcessingSession?
                                        p.This._processSetter.ClearError(elem);
                                    }

                                    forStop.Add(elem.Id);                                    
                                }
                                else
                                {
                                    // 3.2) Защита от зацикливания.
                                    {
                                        var stepCount = elem.GetComponent<StepByStepCycleDetectComponent>();
                                        stepCount.StepCount++;

                                        if (stepCount.StepCount > p.options.CycleLimit)
                                        {
                                            p.This._processSetter.SetError(
                                                elem,
                                                new Exception("Ошибка зацикливания процесса."),
                                                allowRetry: false);
                                        }
                                    }

                                    // 3.3) Сброс признака первого шага.
                                    {
                                        elem.CurrentSession.IsSessionFirstStep = false;
                                    }
                                }
                            }
                        }

                        // 4) Сохраненеи после шага.
                        if (p.options.UseAfterStepSave)
                        {
                            await p.handler.SaveRangeAsync(
                                p.executionGroup.Data.Value,
                                cancellationToken);
                        }

                        foreach (var elem in forStop)
                        {
                            p.executingProcesses.Data.Remove(elem);
                        }
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        // Если ошибка возникла на этапе формирования группы выполнения, то ставим ошибку на весь executingProcesses.
                        p.executionGroup.Data = p.executionGroup.Data
                            ?? new ExecuteGroup(p.executingProcesses.Data);                        

                        // Перезагружаем данные после сброса.
                        if (p.options.UseReloadAfterError)
                        {
                            p.allProcesses.Data = await p.This.LoadAsync(
                                p.handler,
                                p.allProcesses.Data.Values
                                    .Select(e => e.Process.Info)
                                    .ToArray(),
                                p.sessionId,
                                cancellationToken);

                            // Пересобираем группу выполнения после перезагрузки из БД.
                            p.executionGroup.Data = new ExecuteGroup(
                                p.allProcesses.Data.Values
                                    .Where(e => p.executionGroup.Data.Value.Group.ContainsKey(e.Process.Info.Id))
                                    .ToDictionary(e => e.Process.Info.Id, e => e));
                        }                        

                        // Пользовательский хендлер ошибки.
                        await p.handler.OnExceptionRangeAsync(
                            p.executionGroup.Data.Value,
                            ex,
                            cancellationToken);

                        // Условие остановки процесса.
                        var forStop = new List<TId>();
                        foreach (var elem in p.executionGroup.Data.Value.Group.Values)
                        {
                            if (StopCheck(p.This, elem))
                            {
                                ////  Обработка завершена  
                                if (!elem.CurrentSession.CurrentSessionHaveError && elem.CurrentSession.ClearErrorOnSessionEnd)
                                {
                                    // В сессии нет ошибок, тогда отчищаем ошибку.
                                    p.This._processSetter.ClearError(elem);
                                }

                                forStop.Add(elem.Id);
                            }
                        }

                        if (p.options.UseAfterStepSave)
                        {
                            await p.handler.SaveRangeAsync(
                                p.executionGroup.Data.Value,
                                cancellationToken);
                        }

                        foreach (var elem in forStop)
                        {
                            p.executingProcesses.Data.Remove(elem);
                        }
                    },
                    static async (p, ex, cancellationToken) => 
                    {
                        // Перезагружаем данные после сброса.
                        if (p.options.UseReloadAfterError)
                        {
                            p.allProcesses.Data = await p.This.LoadAsync(
                                p.handler,
                                p.allProcesses.Data.Values
                                    .Select(e => e.Process.Info)
                                    .ToArray(),
                                p.sessionId,
                                cancellationToken);

                            // Пересобираем группу выполнения после перезагрузки из БД.
                            p.executionGroup.Data = new ExecuteGroup(
                                p.allProcesses.Data.Values
                                    .Where(e => p.executionGroup.Data.Value.Group.ContainsKey(e.Process.Info.Id))
                                    .ToDictionary(e => e.Process.Info.Id, e => e));
                        }

                        // Хендлер критической ошибки.
                        var forStop = new List<TId>();
                        foreach (var elem in p.executionGroup.Data.Value.Group.Values)
                        {
                            p.This._processSetter.SetError(elem, ex, allowRetry: false);

                            // На критической ошибки останавливаем без условия.
                            // if (StopCheck(p.This, elem))
                            {
                                forStop.Add(elem.Id);
                            }
                        }

                        await p.handler.SaveRangeAsync(
                            p.executionGroup.Data.Value,
                            cancellationToken);

                        foreach (var elem in forStop)
                        {
                            p.executingProcesses.Data.Remove(elem);
                        }
                    },
                    cancellationToken
                    );
            }

            //// 3) Финальное сохранение в конце.
            if (options.UseEndSave)
            {
                var executionGroup = new ExecuteGroup(allProcesses.Data);

                await _isolationService.ExecuteAsync(
                    options.IsolationMode, 
                    (
                        allProcesses, 
                        executingProcesses,
                        executionGroup,
                        
                        options,
                        handler, 
                        This: this,
                        sessionId
                        ),
                    static async (p, cancellationToken) =>
                    {
                        await p.handler.SaveRangeAsync(
                            p.executionGroup,
                            cancellationToken);
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        if (p.options.UseReloadAfterError)
                        {
                            p.allProcesses.Data = await p.This.LoadAsync(
                                p.handler,
                                p.allProcesses.Data.Values
                                    .Select(e => e.Process.Info)
                                    .ToArray(),
                                p.sessionId,
                                cancellationToken);
                            p.executionGroup = new ExecuteGroup(p.allProcesses.Data);
                        }                        

                        await p.handler.OnExceptionRangeAsync(
                            p.executionGroup,
                            ex,
                            cancellationToken);

                        if (p.options.UseAfterStepSave)
                        {
                            await p.handler.SaveRangeAsync(
                                p.executionGroup,
                                cancellationToken);
                        }
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        if (p.options.UseReloadAfterError)
                        {
                            p.allProcesses.Data = await p.This.LoadAsync(
                                p.handler,
                                p.allProcesses.Data.Values
                                    .Select(e => e.Process.Info)
                                    .ToArray(),
                                p.sessionId,
                                cancellationToken);
                            p.executionGroup = new ExecuteGroup(p.allProcesses.Data);
                        }

                        foreach (var elem in p.executingProcesses.Data.Values)
                        {
                            p.This._processSetter.SetError(elem, ex, allowRetry: false);
                        }
                        await p.handler.SaveRangeAsync(
                            p.executionGroup,
                            cancellationToken);
                    },
                    cancellationToken
                    );
            }

            //// 4) Проверка засыпания процессов.
            var wakeupUpdate = await _wakeupService.AfterAsyncSessionHandlerAsync(
                allProcesses.Data.Values,
                cancellationToken);
            await handler.SaveWakeupRangeAsync(wakeupUpdate, cancellationToken);
        }

        private async Task<Dictionary<TId, IProcessContainer<TId>>> LoadAsync(
            IHandler handler,
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var processes = await handler.LoadProcessesWithLockSkipLockedRangeAsync(
                ids.ToArray(), // TODO:
                cancellationToken);

            return processes.ToDictionary(
                e => e.Process.Info.Id,
                e => 
                {
                    e.CurrentSession.SessionId = sessionId;
                    e.AddComponent(new StepByStepCycleDetectComponent());
                    return e;
                }
                );
        }

        #region types

        public interface IHandler
        {
            OptionsDto Options { get; }

            /// <summary>
            /// Загрузгка процессов.
            /// </summary>
            ValueTask<ICollection<IProcessContainer<TId>>> LoadProcessesWithLockSkipLockedRangeAsync(
                ICollection<ProcessInstanceInfoDto<TId>> ids,
                CancellationToken cancellationToken);

            /// <summary>
            /// Сформировать группу процессов на исполнение.
            /// * Могут быть все процессы из батча.
            /// * Могут быть процессы на одном шаге исполнения.
            /// </summary>
            /// <param name="process"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            ValueTask<ExecuteGroup> GetExecutionGroupAsync(
                IDictionary<TId, IProcessContainer<TId>> process,
                CancellationToken cancellationToken);
            
            /// <summary>
            /// Выполнить шаг обработки процессов.
            /// </summary>
            /// <param name="group"></param>
            /// <param name="context"></param>
            /// <param name="cancellationToken"></param>
            ValueTask StepRangeAsync(
                ExecuteGroup group,
                CancellationToken cancellationToken);

            Task SaveRangeAsync(
                ExecuteGroup group,
                CancellationToken cancellationToken);

            /// <summary>
            /// Сохранить состояние <see cref="cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components.IWakeupComponent" /> и статус процесса.
            /// </summary>
            /// <param name="processes"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task SaveWakeupRangeAsync(
                ICollection<IProcessContainer<TId>> process,
                CancellationToken cancellationToken);

            ValueTask OnExceptionRangeAsync(
                ExecuteGroup group,
                Exception ex,
                CancellationToken cancellationToken);
        }

        public readonly record struct ExecuteGroup(
            IDictionary<TId, IProcessContainer<TId>> Group);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="CycleLimit">Лимит повторений выполнений (зацикливания) процесса.</param>
        /// <param name="IsolationMode">Режим изоляции между шагами батча.</param>
        /// <param name="UseAfterStepSave">Вызывать метод сохранения после шага.</param>
        /// <param name="UseEndSave">Вызывать сохраненеи в конце обработки.</param>
        /// <param name="UseReloadAfterError">Перезагружать процессы после сброса.</param>
        public record OptionsDto(
            short CycleLimit,
            IIsolationService.IsolationMode IsolationMode,
            bool UseAfterStepSave,
            bool UseEndSave,
            bool UseReloadAfterError
            );

        #endregion
    }
}
