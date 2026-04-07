using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.OutboxModule.Components
{
    /// <summary>
    /// Компонент процесса для TransactionOutbox.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IOutboxComponent<TId>
    {
        /// <summary>
        /// Идентификатор очереди.
        /// </summary>
        string Queue { get; }

        /// <summary>
        /// Буфер сообщений для обработки (отправки).
        /// Содержить часть или все сообщения, ожидающие обработки.
        /// </summary>
        IList<IOutboxMessageComponent<TId>> Messages { get; }

        int ProcessedCount { get; set; }
    }
}
