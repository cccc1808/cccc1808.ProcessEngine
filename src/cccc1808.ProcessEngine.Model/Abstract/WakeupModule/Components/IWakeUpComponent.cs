using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components
{
    public interface IWakeupComponent<TId>
    {
        #region persist
        
        TId Id { get; }

        bool HaveWakeupEntity { get; set; }

        bool IsAsyncExecuting { get; set; }

        #endregion

        #region inmemory

        /// <summary>
        /// Есть обновления для записи в БД.
        /// Особенность: взводиться, но не сбрасывается (предпологается конец транзакции).
        /// </summary>
        bool NeedUpdate { get; set; }

        #endregion
    }
}