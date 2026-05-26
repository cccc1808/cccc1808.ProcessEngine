using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Отлищает <see cref="Microsoft.EntityFrameworkCore.DbContext.ChangeTracker"/>.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class EFChangeTrackerCompensateService<TId> :
        IChangeTrackerCompensateService<TId>
    {
        private readonly IEFDbContext _dbContext;

        public EFChangeTrackerCompensateService(
            IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var scope = new Scope(_dbContext);
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(scope);
        }

        private class Scope
            : ICompensateService.ICompensateScope
        {
            private readonly IEFDbContext _dbContext;
            private readonly List<Func<CancellationToken, ValueTask>> _manualCompensateHandlers;

            private bool IsCommited { get; set; }            

            public Scope(IEFDbContext dbContext)
            {
                _dbContext = dbContext;
                _manualCompensateHandlers = new List<Func<CancellationToken, ValueTask>>(5);
            }

            public void RegisterManualCompensateHandler(
                Func<CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add(manualCompensateHandler);
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
                    await elem(cancellationToken);
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
