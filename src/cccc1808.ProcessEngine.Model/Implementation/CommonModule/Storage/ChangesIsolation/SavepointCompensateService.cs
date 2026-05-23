using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation
{
    public class SavepointCompensateService<TId>
        : ISavepointCompensateService<TId>
    {
        private readonly ITransactionManager _transactionManager;

        public SavepointCompensateService(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public async ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var transaction = await _transactionManager.CreateSavepointAsync(cancellationToken);
            return new Scope(transaction);
        }

        private record Scope : ICompensateService.ICompensateScope
        {
            private readonly ITransactionManager.ISavepointContainer _savepoint;

            public Scope(
                ITransactionManager.ISavepointContainer transaction)
            {
                _savepoint = transaction;
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                _savepoint.NoAutoRollback();
                return ValueTask.CompletedTask;
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                await _savepoint.RollbackAsync(cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                await _savepoint.DisposeAsync();
            }
        }
    }
}
