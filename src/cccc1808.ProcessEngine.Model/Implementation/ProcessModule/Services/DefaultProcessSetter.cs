using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Services
{
    public class DefaultProcessSetter
        : IProcessSetter
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly Func<short, Exception, DateTimeOffset> _retryDelayFunc;
        private readonly Func<Exception, JsonElement> _formateExceptionFunc;        

        public DefaultProcessSetter(
            IDateTimeProvider dateTimeProvider,
            Func<short, Exception, DateTimeOffset>? retryDelayFunc,
            Func<Exception, JsonElement>? formateExceptionFunc = null)
        {
            _dateTimeProvider = dateTimeProvider;

            _retryDelayFunc = retryDelayFunc
                ?? (
                (count, _) => _dateTimeProvider.UtcNow.Add(count * TimeSpan.FromSeconds(10))
                );
            _formateExceptionFunc = formateExceptionFunc 
                ?? (
                    (ex) => 
                    {
                        // TODO: форматирование 
                        return JsonHelper.ToJsonElement(new { ex.Message, ex.StackTrace });
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

            if (process.WakeupState == WakeupStateEnum.CheckWakeupWithLock && !process.InAsyncExecuting)
            {
                // Если не в асинхронном выполнении, то меняем также компонент.
                var wakeupComponent = process.GetComponent<IWakeupComponent<TId>>();
                wakeupComponent.IsAsyncExecuting = status == ProcessStatusEnum.AsyncExecute;
                wakeupComponent.NeedUpdate = true;
            }
        }

        public void ClearError<TId>(IProcessContainer<TId> process)
        {
            process.CurrentSession.NeedUpdateErrorData = 
                process.CurrentSession.HaveErrorOnStart
                || process.CurrentSession.CurrentSessionHaveError;

            // process.CurrentSession.CurrentSessionHaveError = false;
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
                // Ждем retry триггер.
                SetStatus(process, ProcessStatusEnum.WaitEvent);

                process.Process.StoppedByError = false;
                process.Process.RetryCount = (short)((process.Process.RetryCount ?? 0) + 1);
                
                result = (true, _retryDelayFunc(process.Process.RetryCount.Value, ex));
            }
            else
            {
                // Останавливаем выполнение
                SetStatus(process, ProcessStatusEnum.WaitEvent);

                process.Process.StoppedByError = true;
                
                result = (false, DateTimeOffset.MinValue);
            }

            process.CurrentSession.CurrentSessionHaveError = true;
            process.CurrentSession.NeedUpdateErrorData = true;

            process.Process.Error = new IProcessComponent<TId>.ErrorDto(
                _formateExceptionFunc(ex),
                process.CurrentSession.SessionId,
                _dateTimeProvider.UtcNow
                );            

            return result;
        }

        public void SetSignalCode<TId>(
            IProcessContainer<TId> process, 
            in BitFlagDto value, 
            in BitFlagDto filter)
        {
            if (
                process.Process.SignalCode.Bits != value.Bits 
                || process.Process.SignalCodeFilter.Bits != filter.Bits)
            {
                process.Process.SignalCode = value;
                process.Process.SignalCodeFilter = filter;
            }
        }
    }
}
