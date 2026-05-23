using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components
{
    /// <summary>
    /// Не требуется реализация <see cref="IInmemoryMutableState"/> т.к. компонент крепиться в конце, уже после обработки.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IWakeupComponent<TId>
    {        
        TId Id { get; }

        bool HaveWakeupEntity { get; set; }

        bool IsAsyncExecuting { get; set; }

        /// <summary>
        /// Есть обновления для записи в БД.
        /// Особенность: взводиться, но не сбрасывается (предпологается конец транзакции).
        /// </summary>
        bool NeedUpdate { get; set; }
    }
}