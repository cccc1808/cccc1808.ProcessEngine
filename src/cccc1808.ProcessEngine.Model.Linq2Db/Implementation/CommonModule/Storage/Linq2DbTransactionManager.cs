using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Xml.Linq;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB.Data;

using Npgsql;

namespace cccc1808.ProcessEngine.Model.Linq2Db.Implementation.CommonModule.Storage
{
    public class Linq2DbTransactionManager : ITransactionManager
    {
        private readonly ILinq2DbDataConnection _dataConnection;

        private TransactionContainer? CurrentTransactionContainer { get; set; }

        private SavepointContainer? CurrentSavepointContainer { get; set; }

        protected bool IsReadOnlyTransaction { get; set; }

        private int _savePointNameCounter;

        public Linq2DbTransactionManager(ILinq2DbDataConnection dataConnection)
        {
            _dataConnection = dataConnection;
        }

        public async Task<ITransactionManager.ITransactionContainer> StartTransactionAsync(CancellationToken cancellationToken)
        {
            if (TryGetCurrentTransaction(out _))
            {
                throw new NotSupportedException();
            }

            var dbTransaction = await _dataConnection.DataConnection.BeginTransactionAsync(cancellationToken);
            _savePointNameCounter = 0;
            CurrentTransactionContainer = new TransactionContainer(this, dbTransaction);

            return CurrentTransactionContainer;
        }

        public async Task<ITransactionManager.ISavepointContainer> CreateSavepointAsync(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentTransaction(out var transaction))
            {
                throw new NotSupportedException();
            }
            var typedContainer = (TransactionContainer)transaction;            

            var counter = Interlocked.Increment(ref _savePointNameCounter);
            var name = $@"_auto.{counter}";

            await typedContainer.Transaction.DataConnection.Transaction!.SaveAsync(name);
            return new SavepointContainer(this, _dataConnection.DataConnection, name);
        }       

        public bool TryGetCurrentTransaction(out ITransactionManager.ITransactionContainer transaction)
        {
            transaction = CurrentTransactionContainer!;
            return transaction != null;
        }

        #region types

        private class TransactionContainer
            : ITransactionManager.ITransactionContainer
        {
            private readonly Linq2DbTransactionManager _transactionManager;

            private bool IsUsed { get; set; }
            private bool IsDisposed { get; set; }

            public DataConnectionTransaction Transaction { get; }

            public readonly Dictionary<string, List<(Func<CancellationToken, ValueTask>, Func<CancellationToken, ValueTask>)>> _afterCommitHandlers;

            public TransactionContainer(
                Linq2DbTransactionManager transactionManager,
                DataConnectionTransaction transaction)
            {
                _transactionManager = transactionManager;
                Transaction = transaction;
                IsUsed = false;
                IsDisposed = false;
                _afterCommitHandlers = new Dictionary<string, List<(Func<CancellationToken, ValueTask>, Func<CancellationToken, ValueTask>)>>(2);
            }

            public void NoAction()
            {
                if (IsUsed)
                {
                    throw new NotSupportedException("Действие уже выоплнено, некорректное использование scope.");
                }

                IsUsed = true;
            }

            public async Task CommitAsync(CancellationToken cancellationToken)
            {
                if (IsUsed)
                {
                    throw new NotSupportedException("Действие уже выоплнено, некорректное использование scope.");
                }

                //try
                //{
                //    await _unitOfWork._handlers.BeforeCommitAsync(cancellationToken);
                //}
                //catch (Exception ex)
                //{
                //    Debugger.Break();
                //    throw;
                //}

                await Transaction.CommitAsync(cancellationToken);

                //try
                //{
                //    await _unitOfWork._handlers.AfterTransactionCommitAsync(cancellationToken);
                //}
                //catch (Exception ex)
                //{
                //    Debugger.Break();
                //    throw;
                //}

                IsUsed = true;

                foreach (var elem in _afterCommitHandlers.Values.SelectMany(e => e))
                {
                    await elem.Item1(cancellationToken);
                }
                _afterCommitHandlers.Clear();
            }

            public async Task RollbackAsync(CancellationToken cancellationToken)
            {
                if (IsUsed)
                {
                    throw new NotSupportedException("Действие уже выоплнено, некорректное использование scope.");
                }

                await Transaction.RollbackAsync(cancellationToken);
                foreach (var elem in _afterCommitHandlers.Values.SelectMany(e => e))
                {
                    await elem.Item2(cancellationToken);
                }
                _afterCommitHandlers.Clear();

                IsUsed = true;
            }

            public async ValueTask DisposeAsync()
            {
                if (IsDisposed)
                {
                    return;
                }

                if (!IsUsed)
                {
                    await RollbackAsync(default);
                }

                await Transaction.DisposeAsync();
                _transactionManager.CurrentTransactionContainer = null;
                _transactionManager.IsReadOnlyTransaction = false;

                IsDisposed = true;
            }

            public void AddAfterCommitHandler(
                Func<CancellationToken, ValueTask> commitHandler,
                Func<CancellationToken, ValueTask> rolbackHandler)
            {
                var key = _transactionManager.CurrentSavepointContainer?.Name ?? "-1";

                if (!_afterCommitHandlers.TryGetValue(key, out var savepointCollection))
                {
                    savepointCollection = new List<(Func<CancellationToken, ValueTask>, Func<CancellationToken, ValueTask>)>();
                    _afterCommitHandlers.Add(key, savepointCollection);
                }

                savepointCollection.Add((commitHandler, rolbackHandler));
            }
        }

        private class SavepointContainer
            : ITransactionManager.ISavepointContainer
        {
            private readonly Linq2DbTransactionManager _transactionManager;
            private readonly DataConnection _dataConnection;
            private readonly TransactionContainer _transactionContainer;
            private readonly SavepointContainer? _prevSavepoint;

            public string Name { get; }
            private bool NoAutoRollBack { get; set; }
            private bool IsDisposed { get; set; }

            public SavepointContainer(
                Linq2DbTransactionManager transactionManager,
                DataConnection dataConnection,
                string savePointName
                )
            {
                _transactionManager = transactionManager;
                _dataConnection = dataConnection;

                _transactionContainer = transactionManager.CurrentTransactionContainer ?? throw new Exception("[Bug].");
                _prevSavepoint = transactionManager.CurrentSavepointContainer;

                Name = savePointName;
                NoAutoRollBack = false;
                IsDisposed = false;
            }

            public void NoAutoRollback()
            {
                NoAutoRollBack = true;
            }

            public async Task RollbackAsync(CancellationToken cancellationToken)
            {
                await _transactionContainer.Transaction.DataConnection.Transaction!.RollbackAsync(Name, cancellationToken);

                // Если savepoint откатывается, то созданные в нем хендлеры не будут вызыватся.

                if (_transactionContainer._afterCommitHandlers.TryGetValue(Name, out var savepointCollection))
                {
                    foreach (var elem in savepointCollection)
                    {
                        await elem.Item2(cancellationToken);
                    }
                    _transactionContainer._afterCommitHandlers.Remove(Name);
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (IsDisposed)
                {
                    return;
                }

                if (!NoAutoRollBack)
                {
                    await RollbackAsync(default);
                }

                await _transactionContainer.Transaction.DataConnection.Transaction!.ReleaseAsync(Name);
                IsDisposed = true;

                _transactionManager.CurrentSavepointContainer = _prevSavepoint;
            }
        }

        #endregion
    }
}
