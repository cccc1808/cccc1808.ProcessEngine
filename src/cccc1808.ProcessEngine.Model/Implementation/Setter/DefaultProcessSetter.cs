using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;

namespace cccc1808.ProcessEngine.Model.Implementation.Setter
{
    public class DefaultProcessSetter
        : IProcessSetter
    {
        public void SetStatus<TId>(
            IProcessContainer<TId> process,
            ProcessStatusEnum status)
        {
            process.Process.Status = status;

            if (process.TryGetComponent<IWakeUpComponent>(out var w))
            {
                w.Timestamp = DateTimeOffset.UtcNow;
                if (status == ProcessStatusEnum.AsyncExecute)
                {
                    w.IsAsyncExecuting = true;
                }
                else 
                {
                    w.IsAsyncExecuting = false;
                }
            }
        }

        public void ClearError<TId>(IProcessContainer<TId> process)
        {
            process.Process.ReTryCount = null;
            process.Process.HaveErrorFlag = false;
            process.Process.ErrorJson = null;

            process.CurrentSession.HaveError = false;
            process.CurrentSession.CreateRetryTimer = null;
            process.CurrentSession.RetryTimerCreated = false;
        }

        public void SetError<TId>(
            IProcessContainer<TId> process, 
            Exception ex,
            bool allowRetry)
        {
            if (
                allowRetry
                && (process.Process.ReTryCount ?? 0) <= process.CurrentSession.ReTryLimit
                && !process.Process.HaveErrorFlag
                )
            {
                process.Process.HaveErrorFlag = false;
                process.Process.ReTryCount = (short)((process.Process.ReTryCount ?? 0) + 1);

                process.CurrentSession.HaveError = true;
                process.CurrentSession.CreateRetryTimer = DateTimeOffset.UtcNow + process.Process.ReTryCount * TimeSpan.FromSeconds(10);
                // process.CurrentSession.RetryTimerCreated = false;

                // Тут статус не трогаем.                
            }
            else
            {
                process.Process.HaveErrorFlag = true;

                process.CurrentSession.HaveError = true;
                process.CurrentSession.CreateRetryTimer = null;
                process.CurrentSession.RetryTimerCreated = false;

                // Ожидает таймер
                SetStatus(process, ProcessStatusEnum.WaitEvent);
            }

            process.Process.ErrorJson = JsonSerializer.SerializeToDocument(ex).RootElement.Clone();
        }

        public void SetTimer<TId>(
            IProcessContainer<TId> process, 
            DateTimeOffset date)
        {
            process.Process.TimerDate = date;
            //component.LinkedProcessId = linkedProcess?.linkedId;
            //component.IsProcessOrTimer = linkedProcess?.isProcessOrTimer;
        }
    }
}
