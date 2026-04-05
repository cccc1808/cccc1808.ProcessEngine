using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Handlers
{
    /// <summary>
    /// Хенджлер проверки условия <see cref="ProcessStatusEnum.AsyncExecute"/> / <see cref="ProcessStatusEnum.WaitEvent"/> 
    /// после того, как получена блокировка над wakeup компонентом.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IWakeupCheckHandler<TId>
    {
        ValueTask HandleRangeAsync(
            ICollection<IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken);
    }
}
