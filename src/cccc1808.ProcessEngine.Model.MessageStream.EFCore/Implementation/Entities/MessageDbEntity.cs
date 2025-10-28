using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities
{
    public class MessageDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }
        //public string? IdempotencyId { get; set; }

        /// <summary>
        /// Приоритет сообщения.
        /// </summary>
        public short Priority { get; set; }

        /// <summary>
        /// Порядковый номер сообщения.
        /// </summary>
        public long OrderId { get; set; }

        public TId StreamId { get; set; }
        // public TimerProcessDbEntity<TId> StreamProcess { get; set; }        

        /// <summary>
        /// Ожидает обработки.
        /// </summary>
        public bool IsActive { get; set; }        
    }
}
