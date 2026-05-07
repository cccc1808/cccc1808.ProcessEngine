using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    public class ProcessComponent<TId> : IProcessComponent<TId>
    {
        public ProcessInstanceInfoDto<TId> Info { get; }

        public bool StoppedByError { get; set; }

        public ProcessStatusEnum Status { get; set; }

        public short? RetryCount { get; set; }

        public IProcessComponent<TId>.ErrorDto? Error { get; set; }

        public DateTimeOffset SelectLockTimeout { get; set; }

        public ProcessComponent(
            ProcessInstanceInfoDto<TId> info,
            bool stoppedByError, 
            ProcessStatusEnum status, 
            short? retryCount,
            IProcessComponent<TId>.ErrorDto? error, 
            DateTimeOffset selectLockTimeout)
        {
            Info = info;
            StoppedByError = stoppedByError;
            Status = status;
            RetryCount = retryCount;
            Error = error;
            SelectLockTimeout = selectLockTimeout;
        }
    }
}
