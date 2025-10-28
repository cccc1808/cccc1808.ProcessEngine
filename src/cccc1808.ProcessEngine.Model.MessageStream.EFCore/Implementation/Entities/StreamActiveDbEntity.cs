using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities
{
    /// <summary>
    /// Вспомогательная таблица, чтобы меньше обновлять и блокировать записи процесса стрима.
    /// Позволяет параллельно выполнять стрим и записывать новые сообщения в него.
    /// </summary>
    public class StreamActiveDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        /// <summary>
        /// Взводиться, когда в стриме выставляется флаг активного сообщения.
        /// (В конце обработки стрим сбрасывает флаг).
        /// </summary>
        public bool StreamActiveFlag { get; set; }

        /// <summary>
        /// Дата минимальной задержки.
        /// Используется для оптимизации записи и блокировок.
        /// </summary>
        public long SheduleMinDate { get; set; }
    }
}
