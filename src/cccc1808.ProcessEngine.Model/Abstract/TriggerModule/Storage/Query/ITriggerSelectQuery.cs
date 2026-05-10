using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Query
{
    public interface ITriggerSelectQuery<TId>
    {
        Task<ICollection<SelectDto>> SelectForProcessingAsync(
            int batchSize,
            int parallelLimit,
            int transactionUpdateLimit,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        public readonly record struct SelectDto(
            TId Id,
            // string Key,
            string HandlerKey);
    }
}
