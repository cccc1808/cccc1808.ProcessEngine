using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Services
{
    /// <summary>
    /// Система компенсации, основанная на создании снимка состояния DbContext.ChangeTracker.
    /// </summary>
    internal class ChangeTrackerSnapshotCompensateService
        : IChangeTrackerSnapshotCompensateService
    {
        private readonly IChangeTrackerSnapshotService _changeTrackerSnapshotService;

        public ChangeTrackerSnapshotCompensateService(
            IChangeTrackerSnapshotService changeTrackerSnapshotService)
        {
            _changeTrackerSnapshotService = changeTrackerSnapshotService;
        }

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(CancellationToken cancellationToken)
        {
            var snapshot = _changeTrackerSnapshotService.CaptureState();

            throw new NotImplementedException();
        }

        private record Scope : ICompensateService.ICompensateScope
        {
            private readonly IChangeTrackerSnapshotService.ISubscribe _subscribe;

            public Scope(
                IChangeTrackerSnapshotService.ISubscribe subscribe)
            {
                _subscribe = subscribe;
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                _subscribe.NoRestore();
                return ValueTask.CompletedTask;
            }

            public ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                _subscribe.Restore();
                return ValueTask.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                _subscribe.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
