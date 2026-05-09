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
        Task<IDictionary<TId, IWakeupInfoDto>> AfterSession_LoadStateWithLockAsync(
            ICollection<TId> ids,
            CancellationToken cancellationToken);

        Task<IWakeupContext> Wakeup_LoadStateAsync(
            ICollection<TId> ids,
            bool useShareLock,
            TimeSpan wakeupTryUpdatelockTimeout,
            CancellationToken cancellationToken);

        Task Wakeup_LoadProcessesWithLockAsync(
            IWakeupContext context,
            CancellationToken cancellationToken);

        Task Wakeup_ExecuteAsync(
            IWakeupContext context,
            CancellationToken cancellationToken);


        public interface IWakeupInfoDto 
        {
            TId Id { get; }

            TId ProcessId { get; }

            bool IsAsyncExecuting { get; }
        }

        public interface IWakeupContext 
        {
            IDictionary<TId, IContextEntryDto> Data { get; }

            ICollection<IContextEntryDto> ToWakeupData { get; }
        }

        public interface IContextEntryDto 
        {
            (TId Id, TId ProcessId, bool IsAsyncExecuting) WakeupState { get; }

            (bool StoppedByError, short? RetryCount, ProcessStatusEnum Status)? ProcessState { get; }
        }
    }
}
