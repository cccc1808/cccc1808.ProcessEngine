using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Storage.Query;

namespace cccc1808.ProcessEngine.Model.Implementation.WakeupModule.Storage.Qeury
{
    public static class WakeupServiceQueries<TId>
    {
        public record WakeupInfoDto : IWakeupServiceQueries<TId>.IWakeupInfoDto
        {
            public TId Id { get; init; }

            public TId ProcessId { get; init; }

            public bool IsAsyncExecuting { get; init; }

            public WakeupInfoDto(
                TId id, 
                TId processId, 
                bool isAsyncExecuting)
            {
                Id = id;
                ProcessId = processId;
                IsAsyncExecuting = isAsyncExecuting;
            }
        }


        public class ContextEntryDto : IWakeupServiceQueries<TId>.IContextEntryDto
        {
            public (TId Id, TId ProcessId, bool IsAsyncExecuting) WakeupState { get; }

            public (bool StoppedByError, short? RetryCount, ProcessStatusEnum Status)? ProcessState { get; set; }

            public ContextEntryDto((TId Id, TId ProcessId, bool IsAsyncExecuting) wakeupState, (bool StoppedByError, short? RetryCount, ProcessStatusEnum Status)? processState)
            {
                WakeupState = wakeupState;
                ProcessState = processState;
            }
        }

        public class WakeupContext : IWakeupServiceQueries<TId>.IWakeupContext
        {
            public IDictionary<TId, IWakeupServiceQueries<TId>.IContextEntryDto> Data { get; }

            public ICollection<IWakeupServiceQueries<TId>.IContextEntryDto> ToWakeupData { get; }

            public WakeupContext(
                IDictionary<TId, IWakeupServiceQueries<TId>.IContextEntryDto> data)
            {
                Data = data;
                ToWakeupData = new List<IWakeupServiceQueries<TId>.IContextEntryDto>(data.Count);
            }
        }
    }
}
