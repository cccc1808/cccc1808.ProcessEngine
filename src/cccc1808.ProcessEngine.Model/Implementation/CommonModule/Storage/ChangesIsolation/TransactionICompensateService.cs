using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation
{
    internal class TransactionCompensateService
        : ICompensateService
    {
        private readonly ITransactionManager _transactionManager;

        public TransactionCompensateService(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public async ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await _transactionManager.StartTransactionAsync(cancellationToken);
            return new Scope(transaction);
        }

        private record Scope : ICompensateService.ICompensateScope
        {
            private readonly ITransactionManager.ITransactionContainer _transaction;

            public Scope(
                ITransactionManager.ITransactionContainer transaction)
            {
                _transaction = transaction;
            }

            public async ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                await _transaction.CommitAsync(cancellationToken);
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                await _transaction.RollbackAsync(cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                await _transaction.DisposeAsync();
            }
        }
    }
}
