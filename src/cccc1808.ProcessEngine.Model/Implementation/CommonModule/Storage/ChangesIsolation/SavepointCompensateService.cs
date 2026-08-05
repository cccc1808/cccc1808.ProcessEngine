using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation
{
    public class SavepointCompensateService
        : ISavepointCompensateService
    {
        private readonly ITransactionManager _transactionManager;

        private int ScopeIndex { get; set; }
            = IsolationContainer.TransactionIsolationIndex + 1;

        public SavepointCompensateService(ITransactionManager transactionManager)
        {
            _transactionManager = transactionManager;
        }

        public async ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var transaction = await _transactionManager.CreateSavepointAsync(cancellationToken);
            return new Scope(++ScopeIndex, transaction);
        }

        private class Scope : ICompensateService.ICompensateScope
        {            
            private readonly ITransactionManager.ISavepointContainer _savepoint;
            private readonly List<(object state, Func<int, object, CancellationToken, ValueTask> handler)> _manualCompensateHandlers;

            public int ScopeIndex { get; }

            public Scope(
                int scopeId,
                ITransactionManager.ISavepointContainer transaction)
            {
                ScopeIndex = scopeId;
                _savepoint = transaction;
                _manualCompensateHandlers = new List<(object state, Func<int, object, CancellationToken, ValueTask> handler)>(5);
            }

            public void RegisterManualCompensateHandler(
                object state,
                Func<int, object, CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add((state, manualCompensateHandler));
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                _savepoint.NoAutoRollback();
                return ValueTask.CompletedTask;
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                foreach (var elem in _manualCompensateHandlers)
                {
                    await elem.handler(ScopeIndex, elem.state, cancellationToken);
                }

                await _savepoint.RollbackAsync(cancellationToken);                
            }

            public async ValueTask DisposeAsync()
            {
                await _savepoint.DisposeAsync();

                _manualCompensateHandlers.Clear();
            }            
        }
    }
}
