using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.InboxModule.Components
{
    public interface IInboxComponent<TId>
    {
        string Queue { get; }

        string WakeupTriggerKey { get; }

        IList<IInboxMessageComponent<TId>> Messages { get; }

        #region InMemory

        /// <summary>
        /// Индекс указатель сообщения для обработки из <see cref="Messages"/>.
        /// </summary>
        int CurrentMessageIndex { get; set; }

        #endregion
    }
}
