using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services
{
    public class DefaultProcessSetter
        : IProcessSetter
    {
        private readonly Func<short, Exception, DateTimeOffset> _retryDelayFunc;
        private readonly Func<Exception, JsonElement> _formateExceptionFunc;

        public DefaultProcessSetter(
            Func<short, Exception, DateTimeOffset>? retryDelayFunc,
            Func<Exception, JsonElement>? formateExceptionFunc = null)
        {
            _retryDelayFunc = retryDelayFunc
                ?? (
                (count, _) => DateTimeOffset.UtcNow.Add(count * TimeSpan.FromSeconds(10))
                );
            _formateExceptionFunc = formateExceptionFunc 
                ?? (
                    (ex) => 
                    {
                        // TODO: форматирование 
                        using var json = JsonSerializer.SerializeToDocument(new { ex.Message, ex.StackTrace });
                        return json.RootElement.Clone();
                    }
                    );
        }

        public void StopAsyncProcessingSession<TId>(
            IProcessContainer<TId> process, 
            bool value = true)
        {
            process.CurrentSession.StopAsyncProcessingSession = value;
        }

        public void SetStatus<TId>(
            IProcessContainer<TId> process,
            ProcessStatusEnum status)
        {
            process.Process.Status = status;

            if (process.TryGetComponent<IWakeupComponent>(out var w))
            {
                // Если не в асинхронном выполнении, то меняем также компонент (аккуратно с этим).
                if (!w.InAsyncExecuting)
                {
                    w.IsAsyncExecuting = status == ProcessStatusEnum.AsyncExecute;
                    w.NeedUpdate = true;
                }
            }
        }

        public void ClearError<TId>(IProcessContainer<TId> process)
        {
            process.CurrentSession.NeedUpdateErrorData = 
                process.CurrentSession.HaveErrorOnStart
                || process.CurrentSession.CurrentSessionHaveError;

            process.CurrentSession.CurrentSessionHaveError = false;
            process.Process.RetryCount = null;
            process.Process.StoppedByError = false;
            process.Process.Error = null;
        }

        public (bool IsRetry, DateTimeOffset Timeout) SetError<TId>(
            IProcessContainer<TId> process, 
            Exception ex,
            bool allowRetry)
        {
            (bool IsRetry, DateTimeOffset Timeout) result;
            if (
                allowRetry
                && (process.Process.RetryCount ?? 0) < process.CurrentSession.RetryLimit
                && !process.Process.StoppedByError
                )
            {
                // Тут статус не трогаем.
                process.Process.StoppedByError = false;
                process.Process.RetryCount = (short)((process.Process.RetryCount ?? 0) + 1);

                // Ждем retry триггер.
                SetStatus(process, ProcessStatusEnum.WaitEvent);
                result = (true, _retryDelayFunc(process.Process.RetryCount.Value, ex));
            }
            else
            {
                process.Process.StoppedByError = true;

                // Останавливаем выполнение
                SetStatus(process, ProcessStatusEnum.WaitEvent);
                result = (false, DateTimeOffset.MinValue);
            }

            process.CurrentSession.CurrentSessionHaveError = true;
            process.CurrentSession.NeedUpdateErrorData = true;

            process.Process.Error = new IProcessComponent<TId>.ErrorDto(
                _formateExceptionFunc(ex),
                process.CurrentSession.SessionId,
                DateTimeOffset.UtcNow
                );            

            return result ;
        }       
    }
}
