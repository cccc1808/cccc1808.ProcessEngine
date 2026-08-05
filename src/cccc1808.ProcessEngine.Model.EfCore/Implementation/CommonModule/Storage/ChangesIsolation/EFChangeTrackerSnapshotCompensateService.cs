using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Система компенсации, основанная на создании снимка состояния DbContext.ChangeTracker.
    /// </summary>
    public class EFChangeTrackerSnapshotCompensateService
        : IChangeTrackerSnapshotCompensateService
    {
        private readonly IChangeTrackerSnapshotService _changeTrackerSnapshotService;

        private int ScopeIndex { get; set; }
            = IsolationContainer.TransactionIsolationIndex + 1;

        public EFChangeTrackerSnapshotCompensateService(
            IChangeTrackerSnapshotService changeTrackerSnapshotService)
        {
            _changeTrackerSnapshotService = changeTrackerSnapshotService;
        }

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(CancellationToken cancellationToken)
        {
            var snapshot = _changeTrackerSnapshotService.CaptureState();
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(
                new Scope(
                    snapshot, 
                    ++ScopeIndex));
        }

        private record Scope : ICompensateService.ICompensateScope
        {
            private readonly IChangeTrackerSnapshotService.ISubscribe _subscribe;
            private readonly List<(object state, Func<int, object, CancellationToken, ValueTask> handler)> _manualCompensateHandlers;

            public int ScopeIndex { get; }

            public Scope(
                IChangeTrackerSnapshotService.ISubscribe subscribe, 
                int scopeId)
            {
                _subscribe = subscribe;
                _manualCompensateHandlers = new List<(object state, Func<int, object, CancellationToken, ValueTask> handler)>(5);
                ScopeIndex = scopeId;
            }

            public void RegisterManualCompensateHandler(
                object state,
                Func<int, object, CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add((state, manualCompensateHandler));
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                _subscribe.NoRestore();
                return ValueTask.CompletedTask;
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                foreach (var elem in _manualCompensateHandlers)
                {
                    await elem.handler(ScopeIndex, elem.state, cancellationToken);
                }

                _subscribe.Restore();
            }

            public ValueTask DisposeAsync()
            {
                _subscribe.Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }
}
