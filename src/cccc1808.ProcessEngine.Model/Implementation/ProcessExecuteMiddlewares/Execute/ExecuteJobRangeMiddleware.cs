using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
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

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute
{
    /// <summary>
    /// Выполняет процессы, в которых обработчик состоит из одного шага.
    /// Допускается пакетная обработка.
    /// Предполагается обработка в одной транзакции.
    /// Использует <see cref="ITransactionManager.CreateSavepointAsync(CancellationToken)"/>.
    /// !! Для точечной обработки ошибок требуется ручной перехват.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class ExecuteJobRangeMiddleware<TId>
        : IProcessHandlerMiddleware<TId>
    {
        private readonly IIsolationService _isolationService;
        private readonly Func<IHandler> _jobFactory;
        private readonly IProcessSetter _processSetter;
        private readonly IWakeUpService<TId> _wakeUpService;
        private readonly IProcessContainer_ProcessInstanceInfoDto_Condition<TId, IProcessContainer<TId>> _processEntity_ProcessInstanceInfoDto_Condition;
        private readonly IProcessContainer_ProcessIdDto_Condition<TId, IProcessContainer<TId>> _processEntity_Id_Condition;

        public ExecuteJobRangeMiddleware(
            IIsolationService isolationService,
            Func<IHandler> jobFactory,
            IProcessSetter processSetter,
            IWakeUpService<TId> wakeUpService)
        {
            _isolationService = isolationService;
            _jobFactory = jobFactory;
            _processSetter = processSetter;
            _wakeUpService = wakeUpService;
            _processEntity_ProcessInstanceInfoDto_Condition = new IProcessContainer_ProcessInstanceInfoDto_Condition<TId, IProcessContainer<TId>>();
            _processEntity_Id_Condition = new IProcessContainer_ProcessIdDto_Condition<TId, IProcessContainer<TId>>();            
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

            var handler = _jobFactory();
            var options = handler.Options;

            var processes = await LoadDataAsync(
                handler, 
                ids.First(),
                sessionId,
                cancellationToken);

            await _isolationService.ExecuteAsync(
                options.UseSavepoint
                ? IIsolationService.IsolationMode.DbSavepointAndClearChangeTracker
                : IIsolationService.IsolationMode.No, // Подумать
                (processes, This: this, handler, options, sessionId),
                static async (p, t) => 
                {
                    await p.handler.HandleRangeAsync(p.processes, t);

                    foreach (var elem in p.processes.Values)
                    {
                        if (!elem.CurrentSession.HaveError)
                        {
                            p.This._processSetter.ClearError(elem);
                        }
                    }

                    // Сохраненеи после шага.
                    if (p.options.UseSave)
                    {
                        await p.handler.SaveRangeAsync(
                            p.processes,
                            t);
                    }
                },
                static async (p, ex, t) =>
                {
                    // Тут мы не знаем, какой именно процесс послужил причиной ошибки.

                    if (p.options.UseSavepoint)
                    {
                        p.processes = await p.This.LoadDataAsync(
                            p.handler,
                            p.processes.Values.ApplayProjectionCondition(p.This._processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                            p.sessionId,
                            t);
                    }

                    await p.handler.OnExceptionRangeAsync(
                        p.processes,
                        ex,
                        t);

                    if (p.options.UseSave)
                    {
                        await p.handler.SaveRangeAsync(
                            p.processes,
                            t);
                    }
                },
                static async (p, ex,t) => 
                {
                    if (p.options.UseSavepoint)
                    {
                        p.processes = await p.This.LoadDataAsync(
                            p.handler,
                            p.processes.Values.ApplayProjectionCondition(p.This._processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                            p.sessionId,
                            t);
                    }
                    foreach (var elem in p.processes.Values)
                    {
                        p.This._processSetter.SetError(elem, ex, allowRetry: false);
                    }

                    await p.handler.SaveRangeAsync(
                        p.processes,
                        t);
                },
                cancellationToken
                );

            await _wakeUpService.AfterAsyncSessionHandlerAsync(
                processes.Values,
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

        private async Task<Dictionary<ProcessIdDto<TId>, IProcessContainer<TId>>> LoadDataAsync(
            IHandler handler,
            IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
            Guid sessionId,
            CancellationToken cancellationToken) 
        {
            var data = await handler.LoadWithLockRangeSkipLockedAsync(
                ids,
                cancellationToken);

            return data.ToDictionary(
                _processEntity_Id_Condition.ApplayProjection, 
                e =>
                {
                    e.CurrentSession.SessionId = sessionId;
                    return e;
                });
        }

        public interface IHandler
        {
            OptionsDto Options { get; }

            Task<ICollection<IProcessContainer<TId>>> LoadWithLockRangeSkipLockedAsync(
                IReadOnlyList<ProcessInstanceInfoDto<TId>> ids,
                CancellationToken cancellationToken
                );

            ValueTask HandleRangeAsync(
                IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
                CancellationToken cancellationToken);

            ValueTask OnExceptionRangeAsync(
                IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
                Exception ex,
                CancellationToken cancellationToken);

            /// <summary>
            /// Сохранить состояние процесса целиком.
            /// </summary>
            Task SaveRangeAsync(
                IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
                CancellationToken cancellationToken);

            /// <summary>
            /// Сохранить состояние <see cref="IWakeUpComponent" /> и статус и таймер процесса.
            /// </summary>
            /// <param name="processes"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task SaveWakeupRangeAsync(
                ICollection<IProcessContainer<TId>> processes,
                CancellationToken cancellationToken);
        }

        public record OptionsDto(
            bool UseSavepoint,
            bool UseSave
            );
    }
}
