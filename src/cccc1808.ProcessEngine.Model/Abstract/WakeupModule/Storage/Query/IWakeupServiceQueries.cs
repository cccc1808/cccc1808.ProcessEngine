using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query
{
    public interface IWakeupServiceQueries<TId>
    {
        Task<IDictionary<TId, WakeupDto>> AfterSession_LoadStateWithLockAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken);

        Task<IDictionary<TId, TId>> Wakeup_LoadStateAsync(
            ICollection<TId> ids,
            TimeSpan wakeupTryUpdatelockTimeout,
            CancellationToken cancellationToken);

        Task<ICollection<ProcessInfoDto>> Wakeup_LoadProcessesAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken);

        Task Wakeup_ExecuteAsync(
            ICollection<WakeupDto> processes,
            CancellationToken cancellationToken);

        public readonly record struct ProcessInfoDto(
            TId Id,
            bool StoppedByError,
            short? RetryCount,
            ProcessStatusEnum Status
            );

        public readonly record struct WakeupDto(
            TId Id,
            TId ProcessId,
            bool IsAsyncExecuting);
    }
}
