using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Common.Condition;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services;
using cccc1808.ProcessEngine.Model.Implementation.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute
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
        private readonly IIsolationService _isolationService;
        private readonly IProcessSetter _processSetter;
        private readonly IWakeUpService<TId> _wakeUpService;
        private readonly Func<IServiceProvider, ValueTask<IHandler>> _factory;
        private readonly IProcessContainerConditions<TId> _processContainerConditions;

        public ExecuteStepByStepGroupMiddleware(
            IServiceProvider serviceProvider,
            IIsolationService isolationService,
            IProcessSetter processSetter,
            IWakeUpService<TId> wakeUpService,
            Func<IServiceProvider, ValueTask<IHandler>> factory,
            IProcessContainerConditions<TId> processContainerConditions)
        {
            _serviceProvider = serviceProvider;
            _isolationService = isolationService;
            _processSetter = processSetter;
            _wakeUpService = wakeUpService;
            _factory = factory;
            _processContainerConditions = processContainerConditions;
        }

        public async ValueTask HandleRangeAsync(
            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
            CancellationToken cancellationToken)
        {
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

            // Выполняемые процессы
            var executingProcesses = LinkContainer.Create(
                allProcesses
                    .Data
                    .ToDictionary()
                );

            while (true)
            {
                // Весь набор обработан.
                if (!executingProcesses.Data.Any())
                {
                    break;
                }

                //if (allProcesses.Data.Values
                //    .Any(e => e.TryGetComponent<ISoftTimeoutComponent>(out var component) && component.CheckTimeout()))
                //{
                //    break;
                //}

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
                        {
                            // Условие остановки процесса.
                            foreach (var elem in p.executionGroup.Data.Value.Group.Values)
                            {
                                // Условие асинхронной обработки процесса.
                                if (
                                    elem.CurrentSession.StopAsyncProcessingSession
                                    || !p.This._processContainerConditions.AsyncExecute.Memory.Check(elem, DateTimeOffset.UtcNow))
                                {
                                    if (!elem.CurrentSession.HaveError)
                                    {
                                        p.This._processSetter.ClearError(elem);
                                    }

                                    p.executingProcesses.Data.Remove(elem.Process.Info.Id);
                                }

                                // Защита от зацикливания.
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

                                // Сброс признака первого шага.
                                {
                                    elem.CurrentSession.IsSessionFirstStep = false;
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
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        // Если ошибка возникла на этапе формирования группы выполнения, то ставим ошибку на весь executingProcesses.
                        p.executionGroup.Data = p.executionGroup.Data
                            ?? new ExecuteGroup(
                                "",
                                p.executingProcesses.Data);                        

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
                                p.executionGroup.Data.Value.Key,
                                p.allProcesses.Data.Values
                                    .Where(e => p.executionGroup.Data.Value.Group.ContainsKey(e.Process.Info.Id))
                                    .ToDictionary(e => e.Process.Info.Id, e => e));
                        }                        

                        // Пользовательский хендлер ошибки.
                        await p.handler.OnExceptionRangeAsync(
                            p.executionGroup.Data.Value,
                            ex,
                            cancellationToken);

                        if (p.options.UseAfterStepSave)
                        {
                            await p.handler.SaveRangeAsync(
                                p.executionGroup.Data.Value,
                                cancellationToken);
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
                                p.executionGroup.Data.Value.Key,
                                p.allProcesses.Data.Values
                                    .Where(e => p.executionGroup.Data.Value.Group.ContainsKey(e.Process.Info.Id))
                                    .ToDictionary(e => e.Process.Info.Id, e => e));
                        }

                        // Хендлер критической ошибки.
                        foreach (var elem in p.executionGroup.Data.Value.Group.Values)
                        {
                            p.This._processSetter.SetError(elem, ex, allowRetry: false);
                        }
                        await p.handler.SaveRangeAsync(
                            p.executionGroup.Data.Value,
                            cancellationToken);
                    },
                    cancellationToken
                    );
            }

            // Финальное сохранение в конце.
            if (options.UseEndSave)
            {
                var executionGroup = new ExecuteGroup(
                    "EndSaveAll",
                    allProcesses.Data);

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
                            p.executionGroup = new ExecuteGroup(
                                "EndSaveAll",
                                p.allProcesses.Data);
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
                            p.executionGroup = new ExecuteGroup(
                                "EndSaveAll",
                                p.allProcesses.Data);
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

            await _wakeUpService.AfterAsyncSessionHandlerAsync(
                allProcesses.Data.Values, 
                //(p, t) => 
                //{
                //    return ValueTask.FromResult<ICollection<(TId, bool)>>(
                //        p.Select(e => (e.Id, true)).ToArray()
                //        );
                //},
                async (p, t) =>
                {
                    await handler.SaveWakeupRangeAsync(
                        p,
                        cancellationToken);
                },
                cancellationToken);
        }

        private async Task<Dictionary<ProcessIdDto<TId>, IProcessContainer<TId>>> LoadAsync(
            IHandler handler,
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var processes = await handler.LoadProcessesWithLockSkipLockedRangeAsync(
                ids,
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

            ValueTask<ICollection<IProcessContainer<TId>>> LoadProcessesWithLockSkipLockedRangeAsync(
                IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
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
                IDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> process,
                CancellationToken cancellationToken);
            
            /// <summary>
            /// 
            /// </summary>
            /// <param name="group"></param>
            /// <param name="context"></param>
            /// <param name="cancellationToken"></param>
            /// <returns>Перечень процессов, по которым нужно остановить выполнение.</returns>
            ValueTask StepRangeAsync(
                ExecuteGroup group,
                CancellationToken cancellationToken);

            Task SaveRangeAsync(
                ExecuteGroup group,
                CancellationToken cancellationToken);

            /// <summary>
            /// Сохранить состояние <see cref="IWakeUpComponent" /> и статус и таймер процесса.
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
            string Key, 
            IDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> Group);

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
