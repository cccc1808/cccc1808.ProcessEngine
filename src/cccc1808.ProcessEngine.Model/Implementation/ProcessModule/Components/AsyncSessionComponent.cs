using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    public class AsyncSessionComponent
        : IAsyncSessionComponent, 
        IInmemoryMutableState
    {
        public Guid SessionId { get; set; }

        public bool IsSessionFirstStep { get; set; }

        public bool CurrentSessionHaveError { get; set; }

        public short RetryLimit { get; set; }

        public bool StopAsyncProcessingSession { get; set; }

        public bool NeedUpdateErrorData { get; set; }

        public bool HaveErrorOnStart { get; private set; }

        public bool ClearErrorOnSessionEnd { get; set; }

        public AsyncSessionComponent(
            Guid sessionId,
            bool isSessionFirstStep,
            bool currentSessionHaveError,
            short retryLimit,
            bool stopAsyncProcessingSession,
            bool needUpdateErrorData,
            bool haveErrorOnStart,
            bool clearErrorOnSessionEnd)
        {
            SessionId = sessionId;
            IsSessionFirstStep = isSessionFirstStep;
            CurrentSessionHaveError = currentSessionHaveError;
            RetryLimit = retryLimit;
            StopAsyncProcessingSession = stopAsyncProcessingSession;
            NeedUpdateErrorData = needUpdateErrorData;
            HaveErrorOnStart = haveErrorOnStart;
            ClearErrorOnSessionEnd = clearErrorOnSessionEnd;
        }

        #region IInmemoryMutableState

        public IInmemoryMutableState.ISnapshot Capture()
        {
            return IInmemoryMutableState.JsonSnapshot.Create(this);
        }

        public void Restore(IInmemoryMutableState.ISnapshot snapshot)
        {
            var snap = IInmemoryMutableState.JsonSnapshot.Restore<AsyncSessionComponent>((IInmemoryMutableState.JsonSnapshot)snapshot);
            SessionId = snap.SessionId;
            IsSessionFirstStep = snap.IsSessionFirstStep;
            CurrentSessionHaveError = snap.CurrentSessionHaveError;
            RetryLimit = snap.RetryLimit;
            StopAsyncProcessingSession = snap.StopAsyncProcessingSession;
            NeedUpdateErrorData = snap.NeedUpdateErrorData;
            HaveErrorOnStart = snap.HaveErrorOnStart;
        }

        #endregion
    }
}
