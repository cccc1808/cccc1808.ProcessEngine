using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Components
{
    public interface IWakeUpComponent
    {
        #region persist
        
        bool IsAsyncExecuting { get; set; }        

        #endregion

        #region inmemory

        /// <summary>
        /// Внутренний костыль.
        /// </summary>
        bool InAsyncExecuting { get; set; }

        /// <summary>
        /// Есть обновления для записи в БД.
        /// Особенность: взводиться, но не сбрасывается (предпологается конец транзакции).
        /// </summary>
        bool NeedUpdate { get; set; }

        /// <summary>
        /// Результат проверки условия после бокировки (True - не засыпаем).
        /// </summary>
        bool HandlerResult { get; set; }

        #endregion
    }
}
