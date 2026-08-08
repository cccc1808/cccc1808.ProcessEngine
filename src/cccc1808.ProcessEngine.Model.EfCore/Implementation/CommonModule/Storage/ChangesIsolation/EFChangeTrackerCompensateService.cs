using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation
{
    public class EFChangeTrackerCompensateService :
        IChangeTrackerCompensateService
    {
        private readonly IEFDbContext _dbContext;

        private int ScopeIndex { get; set; }
            = IsolationContainer.TransactionIsolationIndex + 1;

        public EFChangeTrackerCompensateService(IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var scope = new Scope(_dbContext, ++ScopeIndex);
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(scope);
        }

        private class Scope
            : ICompensateService.ICompensateScope
        {
            private readonly IEFDbContext _dbContext;
            private readonly List<(object state, Func<int, object, CancellationToken, ValueTask> handler)> _manualCompensateHandlers;

            private bool IsCommited { get; set; }

            public int ScopeIndex { get; }

            public Scope(
                IEFDbContext dbContext,
                int scopeId)
            {
                _dbContext = dbContext;
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
                if (IsCommited)
                {
                    throw new InvalidOperationException();
                }

                IsCommited = true;
                return ValueTask.CompletedTask;
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                if (IsCommited)
                {
                    throw new InvalidOperationException();
                }

                foreach (var elem in _manualCompensateHandlers)
                {
                    await elem.handler(ScopeIndex, elem.state, cancellationToken);
                }

                _dbContext.DbContext.ChangeTracker.Clear();
            }

            public async ValueTask DisposeAsync()
            {
                if (IsCommited)
                {
                    return;
                }

                await CompensateAsync(default);
            }            
        }
    }
}
