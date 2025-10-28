using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation
{
    public class EFChangeTrackerCompensateService<TDbContext> :
        IChangeTrackerCompensateService
        where TDbContext: DbContext
    {
        private readonly TDbContext _dbContext;

        public EFChangeTrackerCompensateService(TDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var scope = new Scope(_dbContext);
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(scope);
        }

        private class Scope
            : IChangeTrackerCompensateService.ICompensateScope
        {
            private readonly TDbContext _dbContext;

            private bool IsCommited { get; set; }

            public Scope(TDbContext dbContext)
            {
                _dbContext = dbContext;
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

            public ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                if (IsCommited)
                {
                    throw new InvalidOperationException();
                }

                _dbContext.ChangeTracker.Clear();
                return ValueTask.CompletedTask;
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
