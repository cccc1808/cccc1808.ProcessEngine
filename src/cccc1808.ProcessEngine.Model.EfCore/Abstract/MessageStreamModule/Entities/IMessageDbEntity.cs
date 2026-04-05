using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Entities
{ 
    /// <summary>
    /// Набор свойство сообщения.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IMessageDbEntity<TId> : IProcessLinked<TId>
    {
        TId Id { get; set; }
        //public string? IdempotencyId { get; set; }

        /// <summary>
        /// Приоритет сообщения.
        /// </summary>
        short Priority { get; set; }

        /// <summary>
        /// Порядковый номер сообщения.
        /// </summary>
        long OrderId { get; set; }

        /// <summary>
        /// Ожидает обработки.
        /// </summary>
        bool IsActive { get; set; }
    }
}
