using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    public class AsyncSessionComponent
        : IAsyncSessionComponent
    {
        public Guid SessionId { get; set; }

        public bool IsSessionFirstStep { get; set; }

        public bool CurrentSessionHaveError { get; set; }

        public short RetryLimit { get; set; }

        public bool StopAsyncProcessingSession { get; set; }

        public bool NeedUpdateErrorData { get; set; }

        public bool HaveErrorOnStart { get; }


        public AsyncSessionComponent(
            Guid sessionId, 
            bool isSessionFirstStep,
            bool currentSessionHaveError, 
            short retryLimit, 
            bool stopAsyncProcessingSession,
            bool needUpdateErrorData,
            bool haveErrorOnStart)
        {
            SessionId = sessionId;
            IsSessionFirstStep = isSessionFirstStep;
            CurrentSessionHaveError = currentSessionHaveError;
            RetryLimit = retryLimit;
            StopAsyncProcessingSession = stopAsyncProcessingSession;
            NeedUpdateErrorData = needUpdateErrorData;
            HaveErrorOnStart = haveErrorOnStart;
        }
    }
}
