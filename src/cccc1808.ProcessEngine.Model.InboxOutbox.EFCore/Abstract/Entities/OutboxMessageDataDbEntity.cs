using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    public class OutboxMessageDataDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public string Key { get; set; }

        public JsonElement Body { get; set; }

        public JsonElement Headers { get; set; }

        public DateTimeOffset? SendDate { get; set; }

        /// <summary>
        /// На проработке.
        /// </summary>
        public StatusEnum Status {  get; set; }

        public int Partition {  get; set; }

        //public ICollection<IInboxMessageEntity<TId>> Delivery { get; set; }

        //public ICollection<IInboxMessageEntity<TId>> Responses { get; set; }

        public enum StatusEnum
        {
            WaitToSend,
            WaitDelivery,
            WaitResponse,
            Complete,
        }
    }
}
