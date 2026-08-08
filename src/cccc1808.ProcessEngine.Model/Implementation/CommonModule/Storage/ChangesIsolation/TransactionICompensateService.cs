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

        private int ScopeIndex { get; set; }
            = IsolationContainer.TransactionIsolationIndex + 1;

        public TransactionCompensateService(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public async ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await _transactionManager.StartTransactionAsync(cancellationToken);
            return new Scope(transaction, ++ScopeIndex);
        }

        private class Scope : ICompensateService.ICompensateScope
        {
            private readonly ITransactionManager.ITransactionContainer _transaction;
            private readonly List<(object state, Func<int, object, CancellationToken, ValueTask> handler)> _manualCompensateHandlers;

            public int ScopeIndex { get; }

            public Scope(
                ITransactionManager.ITransactionContainer transaction,
                int scopeId)
            {
                _transaction = transaction;
                _manualCompensateHandlers = new List<(object state, Func<int, object, CancellationToken, ValueTask> handler)>(5);
                ScopeIndex = scopeId;
            }

            public void RegisterManualCompensateHandler(
                object state,
                Func<int, object, CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add((state, manualCompensateHandler));
            }

            public async ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                await _transaction.CommitAsync(cancellationToken);
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                foreach (var elem in _manualCompensateHandlers)
                {
                    await elem.handler(ScopeIndex, elem.state, cancellationToken);
                }

                await _transaction.RollbackAsync(cancellationToken);                
            }

            public async ValueTask DisposeAsync()
            {
                await _transaction.DisposeAsync();

                _manualCompensateHandlers.Clear();
            }            
        }
    }
}
