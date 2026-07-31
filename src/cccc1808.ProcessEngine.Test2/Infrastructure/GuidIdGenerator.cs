using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal class GuidIdGenerator : IIdGenerator<Guid>
    {
        public ValueTask<Guid> NextAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(Guid.NewGuid());

        public ValueTask<Queue<Guid>> NextRangeAsync(
            int count, 
            CancellationToken cancellationToken)
        {
            var result = new Queue<Guid>(count);
            for (var i = 0; i < count; i++) 
            {
                result.Enqueue(Guid.NewGuid());
            }
            return ValueTask.FromResult(result);
        }
    }
}
