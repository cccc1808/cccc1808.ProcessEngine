using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly Func<short, Exception, DateTimeOffset> _retryDelayFunc;

        public DefaultProcessSetter(
            Func<short, Exception, DateTimeOffset>? retryDelayFunc)
        {
            _retryDelayFunc = retryDelayFunc
                ?? (
                (count, _) => DateTimeOffset.UtcNow.Add(count * TimeSpan.FromSeconds(10))
                );
        }

        public void SetStatus<TId>(
            IProcessContainer<TId> process,
            ProcessStatusEnum status)
        {
            process.Process.Status = status;

            if (process.TryGetComponent<IWakeUpComponent>(out var w))
            {
                if (!w.InAsyncExecuting)
                {
                    w.IsAsyncExecuting = status == ProcessStatusEnum.AsyncExecute;
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
                process.Process.TimerDate = _retryDelayFunc(process.Process.ReTryCount.Value, ex);
                
                process.CurrentSession.HaveError = true;
            }
            else
            {
                process.Process.HaveErrorFlag = true;
                process.CurrentSession.HaveError = true;
            }

            // TODO: форматирование
            using var json = JsonSerializer.SerializeToDocument(new { ex.Message, ex.StackTrace });
            process.Process.ErrorJson = json.RootElement.Clone();
        }

        public void SetTimer<TId>(
            IProcessContainer<TId> process, 
            DateTimeOffset date)
        {
            process.Process.TimerDate = date;

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
