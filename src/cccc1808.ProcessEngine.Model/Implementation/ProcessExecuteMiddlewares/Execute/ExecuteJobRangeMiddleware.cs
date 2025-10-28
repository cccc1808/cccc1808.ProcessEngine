using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
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
using cccc1808.ProcessEngine.Model.Implementation.Storage;

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
        private readonly IProcessContainer_ProcessInstanceInfoDto_Condition<TId, IProcessContainer<TId>> _processEntity_ProcessInstanceInfoDto_Condition;
        private readonly IProcessContainer_ProcessIdDto_Condition<TId, IProcessContainer<TId>> _processEntity_Id_Condition;

        public ExecuteJobRangeMiddleware(
            IIsolationService isolationService,
            Func<IHandler> jobFactory,
            IProcessSetter processSetter
            )
        {
            _isolationService = isolationService;
            _jobFactory = jobFactory;
            _processSetter = processSetter;
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
                (1,2),
                static async (p,t) => 
                {
                    await handler.HandleRangeAsync(processes, cancellationToken);

                    foreach (var elem in processes.Values)
                    {
                        if (!elem.CurrentSession.HaveError)
                        {
                            _processSetter.ClearError(elem);
                        }
                    }

                    // Сохраненеи после шага.
                    if (options.UseSave)
                    {
                        await handler.SaveRangeAsync(
                            processes,
                            cancellationToken);
                    }
                },
                static async (p, ex, t) =>
                {
                    // Тут мы не знаем, какой именно процесс послужил причиной ошибки.

                    if (options.UseSavepoint)
                    {
                        processes = await LoadDataAsync(
                            handler,
                            processes.Values.ApplayProjectionCondition(_processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                            sessionId,
                            cancellationToken);
                    }

                    await handler.OnExceptionRangeAsync(
                        processes,
                        ex,
                        cancellationToken);

                    if (options.UseSave)
                    {
                        await handler.SaveRangeAsync(
                            processes,
                            cancellationToken);
                    }
                },
                static async (p, ex,t) => 
                {
                    if (options.UseSavepoint)
                    {
                        processes = await LoadDataAsync(
                            handler,
                            processes.Values.ApplayProjectionCondition(_processEntity_ProcessInstanceInfoDto_Condition).ToArray(),
                            sessionId,
                            cancellationToken);
                    }

                    var saveException = new AggregateException(ex, ex2);
                    foreach (var elem in processes.Values)
                    {
                        _processSetter.SetError(elem, saveException);
                    }

                    await handler.SaveRangeAsync(
                        processes,
                        cancellationToken);
                },
                cancellationToken
                );            
        }

        private async Task<IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>>> LoadDataAsync(
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

            Task SaveRangeAsync(
                IReadOnlyDictionary<ProcessIdDto<TId>, IProcessContainer<TId>> processes,
                CancellationToken cancellationToken);
        }

        public record OptionsDto(
            bool UseSavepoint,
            bool UseSave
            );
    }
}
