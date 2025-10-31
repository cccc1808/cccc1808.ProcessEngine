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
                if (!w.InAsyncExecuting)
                {
                    if (status == ProcessStatusEnum.AsyncExecute)
                    {
                        w.IsAsyncExecuting = true;
                    }
                    else
                    {
                        w.IsAsyncExecuting = false;
                    }

                    w.Timestamp = DateTimeOffset.UtcNow;
                    w.NeedUpdate = true;
                }                
            }
        }

        public void ClearError<TId>(IProcessContainer<TId> process)
        {
            process.Process.ReTryCount = null;
            process.Process.HaveErrorFlag = false;
            process.Process.ErrorJson = null;

            process.CurrentSession.HaveError = false;
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
                // Тут статус не трогаем.
                process.Process.HaveErrorFlag = false;
                process.Process.ReTryCount = (short)((process.Process.ReTryCount ?? 0) + 1);
                process.Process.TimerDate = DateTimeOffset.UtcNow.Add(
                    process.Process.ReTryCount.Value * TimeSpan.FromSeconds(10));
                
                process.CurrentSession.HaveError = true;
            }
            else
            {
                process.Process.HaveErrorFlag = true;

                process.CurrentSession.HaveError = true;
            }

            using var json = JsonSerializer.SerializeToDocument(ex);
            process.Process.ErrorJson = json.RootElement.Clone();
        }

        public void SetTimer<TId>(
            IProcessContainer<TId> process, 
            DateTimeOffset date)
        {
            process.Process.TimerDate = date;
            //component.LinkedProcessId = linkedProcess?.linkedId;
            //component.IsProcessOrTimer = linkedProcess?.isProcessOrTimer;

            if (process.TryGetComponent<IWakeUpComponent>(out var w))
            {
                if (!w.InAsyncExecuting)
                {
                    w.TimerDate = date;

                    w.Timestamp = DateTimeOffset.UtcNow;
                    w.NeedUpdate = true;
                }
            }
        }
    }
}
