using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query
{
    public interface ITriggerSelectQuery<TId>
    {
        IContextState BuildContext(IOptions options);

        Task<ICollection<SelectDto>> SelectForProcessingAsync(
            IContextState contextState,
            CancellationToken cancellationToken);

        Task UnholdSelectLockAsync(
            ICollection<TId> ids, 
            CancellationToken cancellationToken);

        public readonly record struct SelectDto(
            TId Id,
            // string Key,
            string HandlerKey);


        public interface IOptions 
        {
            
        }

        public interface IContextState 
        {
            void SetFreeSlots(int freeSlotsCount);
        }
    }
}
