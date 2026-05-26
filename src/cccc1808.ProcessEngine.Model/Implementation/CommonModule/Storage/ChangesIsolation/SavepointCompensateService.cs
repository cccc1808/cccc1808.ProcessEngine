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

        private class Scope : ICompensateService.ICompensateScope
        {
            private readonly ITransactionManager.ISavepointContainer _savepoint;
            private readonly List<Func<CancellationToken, ValueTask>> _manualCompensateHandlers;

            public Scope(
                ITransactionManager.ISavepointContainer transaction)
            {
                _savepoint = transaction;
                _manualCompensateHandlers = new List<Func<CancellationToken, ValueTask>>(5);
            }

            public void RegisterManualCompensateHandler(
                Func<CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add(manualCompensateHandler);
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
                    await elem(cancellationToken);
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
