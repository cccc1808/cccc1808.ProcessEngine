using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Система компенсации, основанная на создании снимка состояния DbContext.ChangeTracker.
    /// </summary>
    public class EFChangeTrackerSnapshotCompensateService<TId>
        : InMemoryChangeTrackerSnapshotCompensateService<TId>,
        IChangeTrackerSnapshotCompensateService<TId>
    {
        private readonly IChangeTrackerSnapshotService _changeTrackerSnapshotService;

        public EFChangeTrackerSnapshotCompensateService(
            IChangeTrackerSnapshotService changeTrackerSnapshotService)
        {
            _changeTrackerSnapshotService = changeTrackerSnapshotService;
        }

        public override async ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            // Inmemory snapshot.
            var baseScope = await base.StartScopeAsync(processes, cancellationToken);

            // EF change tracker snapshot.
            var snapshot = _changeTrackerSnapshotService.CaptureState();
            var scope = new Scope(snapshot);

            return new ICompensateService.CompoundScope(
                [baseScope, scope]
                );
        }

        private record Scope : ICompensateService<TId>.ICompensateScope
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
