using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Services.ProcessExecuteMiddlewares;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
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
        private readonly Func<IServiceProvider, ValueTask<IHandler>> _factory;
        private readonly IProcessContainer_ProcessIdDto_Condition<TId, IProcessContainer<TId>> _processEntity_ProcessIdDto_Condition;
        private readonly IProcessContainer_ProcessInstanceInfoDto_Condition<TId, IProcessContainer<TId>> _processEntity_ProcessInstanceInfoDto_Condition;
        private readonly IProcessContainer_AsyncExecute_Condition<TId> _processEntity_AsyncExecute_Condition;

        public ExecuteStepByStepGroupMiddleware(
            IServiceProvider serviceProvider,
            IIsolationService isolationService,
            IProcessSetter processSetter, 
            Func<IServiceProvider, ValueTask<IHandler>> factory)
        {
            _serviceProvider = serviceProvider;
            _isolationService = isolationService;
            _processSetter = processSetter;
            _factory = factory;
            _processEntity_ProcessIdDto_Condition = new IProcessContainer_ProcessIdDto_Condition<TId, IProcessContainer<TId>>();
            _processEntity_ProcessInstanceInfoDto_Condition = new IProcessContainer_ProcessInstanceInfoDto_Condition<TId, IProcessContainer<TId>>();
            _processEntity_AsyncExecute_Condition = new IProcessContainer_AsyncExecute_Condition<TId>();
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

            var allProcesses = await LoadAsync(
                handler,
                ids.First(),
                sessionId,
                cancellationToken);

            //allProcesses = allProcesses
            //    // Если процесс уже завершился.
            //    .Where(e => _processEntity_AsyncExecute_Condition.Check(e.Value, default))
            //    .ToDictionary();

            if (allProcesses.Count == 0)
            {
                return;
            }

            // Выполняемые процессы
            var executingProcesses = allProcesses
                .ToDictionary();

            while (true)
            {
                // Весь набор обработан.
                if (!executingProcesses.Any())
                {
                    break;
                }

                ExecuteGroup? executionGroup = null;
                await _isolationService.ExecuteAsync(
                    options.UseSavepoint 
                     ? IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker
                     : IIsolationService.IsolationMode.ClearChangeTracker,
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
                        // Формирование группы выполнения.
                        p.executionGroup = await p.handler.GetExecutionGroupAsync(
                            p.executingProcesses,
                            cancellationToken);

                        // Шаг.
                        var stopIds = await p.handler.StepRangeAsync(
                            p.executionGroup.Value,
                            cancellationToken);

                        // Проверка завершенных процессов
                        {
                            // 1) Разработчик указал прервать выполнение в текущей сессии. (Допустимо, что они AsyncExecuting).
                            foreach (var elem in stopIds)
                            {
                                var process = p.executingProcesses[elem];
                                if (!process.CurrentSession.HaveError)
                                {
                                    p.This._processSetter.ClearError(process);
                                }

                                p.executingProcesses.Remove(elem);
                            }

                            // 2) Условие остановки процесса.
                            foreach (var elem in p.executionGroup.Value.Group.Values)
                            {
                                // Защита от зацикливания.
                                {
                                    var stepCount = elem.GetComponent<StepByStepCycleDetectComponent>();
                                    stepCount.StepCount++;

                                    if (stepCount.StepCount > p.options.CycleLimit)
                                    {
                                        p.This._processSetter.SetError(
                                            elem,
                                            new Exception("Ошибка зацикливания процесса."));
                                    }
                                }

                                if (!p.This._processEntity_AsyncExecute_Condition.Check(elem, default))
                                {
                                    if (!elem.CurrentSession.HaveError)
                                    {
                                        p.This._processSetter.ClearError(elem);
                                    }

                                    p.executingProcesses.Remove(elem.Process.Info.Id);
                                }

                                {
                                    elem.CurrentSession.IsSessionFirstStep = false;
                                }
                            }
                        }

                        // Сохраненеи после шага.
                        if (p.options.UseAfterGroupSave)
                        {
                            await p.handler.SaveRangeAsync(
                                p.executionGroup.Value,
                                cancellationToken);
                        }
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        // Если ошибка возникла на этапе формирования группы выполнения, то ставим ошибку на весь executingProcesses.
                        p.executionGroup = p.executionGroup
                            ?? new ExecuteGroup(
                                "",
                                p.executingProcesses);

                        // Пользовательский хендлер ошибки

                        // Перезагружаем данные после сброса.
                        p.allProcesses = await p.This.LoadAsync(
                            p.handler,
                            p.allProcesses.Values.ApplayProjectionCondition(p.This._processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                            p.sessionId,
                            cancellationToken);
                        // Пересобираем группу выполнения после перезагрузки из БД.
                        p.executionGroup = new ExecuteGroup(
                            p.executionGroup.Value.Key,
                            p.allProcesses.Values
                                .Where(e => p.executionGroup.Value.Group.ContainsKey(e.Process.Info.Id))
                                .ToDictionary(e => e.Process.Info.Id, e => e));

                        // Пользовательский хендлер ошибки.
                        await p.handler.OnExceptionRangeAsync(
                            p.executionGroup.Value,
                            ex,
                            cancellationToken);

                        if (p.options.UseAfterGroupSave)
                        {
                            await p.handler.SaveRangeAsync(
                                p.executionGroup.Value,
                                cancellationToken);
                        }
                    },
                    static async (p, ex, cancellationToken) => 
                    {
                        // Хендлер критической ошибки.

                        // Перезагружаем данные после сброса.
                        p.allProcesses = await p.This.LoadAsync(
                            p.handler,
                            p.allProcesses.Values.ApplayProjectionCondition(p.This._processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                            p.sessionId,
                            cancellationToken);
                        p.executionGroup = new ExecuteGroup(
                            p.executionGroup.Value.Key,
                            p.allProcesses.Values
                                .Where(e => p.executionGroup.Value.Group.ContainsKey(e.Process.Info.Id))
                                .ToDictionary(e => e.Process.Info.Id, e => e));
                        
                        foreach (var elem in p.executingProcesses.Values)
                        {
                            p.This._processSetter.SetError(elem, ex);
                        }
                        await p.handler.SaveRangeAsync(
                            p.executionGroup.Value,
                            cancellationToken);
                    },
                    cancellationToken
                    );
            }

            // Финальное сохранение в конце.
            {
                var executionGroup = new ExecuteGroup(
                    "EndSaveAll",                            
                    allProcesses);
                
                await _isolationService.ExecuteAsync(
                    options.UseSavepoint
                        ? IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker
                        : IIsolationService.IsolationMode.ClearChangeTracker, // Предполагается атомарное сохранение внутри.
                    (1,2),
                    static async (p, t) => 
                    {  
                        await handler.SaveRangeAsync(
                            executionGroup,
                            cancellationToken
                            );
                    },
                    static async (p, ex, t) =>
                    {
                        allProcesses = await LoadAsync(
                                handler,
                                allProcesses.Values.ApplayProjectionCondition(_processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                                sessionId,
                                cancellationToken);
                        executionGroup = new ExecuteGroup(
                            "EndSaveAll",
                            allProcesses);

                        await handler.OnExceptionRangeAsync(
                                    executionGroup,
                                    ex,
                                    cancellationToken);

                        await handler.SaveRangeAsync(
                            executionGroup,
                            cancellationToken);
                    },
                    static async (p, ex, t) => 
                    {
                        allProcesses = await LoadAsync(
                                    handler,
                                    allProcesses.Values.ApplayProjectionCondition(_processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                                    sessionId,
                                    cancellationToken);
                        executionGroup = new ExecuteGroup(
                            "EndSaveAll",
                            allProcesses);

                        var saveException = new AggregateException(ex, ex2);
                        foreach (var elem in executingProcesses.Values)
                        {
                            _processSetter.SetError(elem, saveException);
                        }
                        await handler.SaveRangeAsync(
                            executionGroup,
                            cancellationToken);
                    }
                    );
            }
        }

        private async Task<IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>>> LoadAsync(
            IHandler handler,
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
            Guid sessionId,
            CancellationToken cancellationToken)
        {
            var processes = await handler.LoadProcessesWithLockSkipLockedRangeAsync(
                ids,
                cancellationToken);

            return processes.ToDictionary(
                _processEntity_ProcessIdDto_Condition.ApplayProjection,
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
            ValueTask<ICollection<ProcessIdDto<TId>>> StepRangeAsync(
                ExecuteGroup group,
                CancellationToken cancellationToken);

            Task SaveRangeAsync(
                ExecuteGroup group,
                CancellationToken cancellationToken);

            ValueTask OnExceptionRangeAsync(
                ExecuteGroup group,
                Exception ex,
                CancellationToken cancellationToken);
        }

        public readonly record struct ExecuteGroup(
            string Key, 
            IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> Group);

        public record OptionsDto(
            short CycleLimit,
            bool UseSavepoint,
            bool UseAfterGroupSave,
            bool UseEndSave
            );

        #endregion
    }
}
