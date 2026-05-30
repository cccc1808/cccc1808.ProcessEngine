using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;

namespace cccc1808.ProcessEngine.Test3.Infrastructure
{
    internal class GuidIdGenerator : IIdGenerator<Guid>
    {
        public ValueTask<Guid> NextAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(Guid.NewGuid());
    }
}
