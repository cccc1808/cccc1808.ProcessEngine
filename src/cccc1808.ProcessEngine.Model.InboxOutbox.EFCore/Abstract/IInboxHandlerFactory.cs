using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Dto;
using cccc1808.ProcessEngine.Model.InboxOutbox.Abstract.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.Abstract
{
    public interface IInboxHandlerFactory<TId>
    {
        IHandler GetHandler(
            InboxStreamDataDbEntity<TId> stream);

        public interface IHandler 
        {
            ValueTask HandleAsync(
                InboxStreamDataDbEntity<TId> stream,
                ICollection<MessageDto> messages,
                CancellationToken cancellationToken
                );
        }
    }
}
