using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.MessageStream.EFCore.Abstract;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities
{
    public class OutboxMessageDbEntity<TId>
        : IMessageDbEntity<TId>
    {
        #region IMessageDbEntity

        public TId Id { get; set; }

        public int Partition { get; set; }

        public short Priority { get; set; }

        public long OrderId { get; set; }

        public TId StreamProcessId { get; set; }

        public bool IsActive { get; set; }

        #endregion

        public string Key { get; set; }

        public JsonElement Body { get; set; }

        public JsonElement Headers { get; set; }

        public DateTimeOffset? SendDate { get; set; }

        /// <summary>
        /// На проработке.
        /// </summary>
        public StatusEnum Status {  get; set; }        

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
