using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;

using Microsoft.EntityFrameworkCore.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.CommonModule.Storage
{
    public class EFTransactionManager
        : ITransactionManager
    {
        protected readonly IEFDbContext _dbContext;

        private TransactionContainer? CurrentTransactionContainer { get; set; }

        private SavepointContainer? CurrentSavepointContainer { get; set; }

        protected bool IsReadOnlyTransaction { get; set; }

        private int _savePointNameCounter;


        public EFTransactionManager(
            IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }


        #region IUnitOfWorkProvider

        #region tran

        public virtual async Task<ITransactionManager.ITransactionContainer> StartTransactionAsync(
            CancellationToken cancellationToken)
        {
            if (TryGetCurrentTransaction(out _))
            {
                throw new NotSupportedException();
            }

            var dbTransaction = await _dbContext.DbContext.Database.BeginTransactionAsync(cancellationToken);
            _savePointNameCounter = 0;
            CurrentTransactionContainer = new TransactionContainer(this, dbTransaction);

            return CurrentTransactionContainer;
        }

        public bool TryGetCurrentTransaction(out ITransactionManager.ITransactionContainer transaction)
        {
            transaction = CurrentTransactionContainer!;
            return transaction is not null;
        }

        #endregion

        #region Savepoint

        public async Task<ITransactionManager.ISavepointContainer> CreateSavepointAsync(CancellationToken cancellationToken)
        {
            if (!TryGetCurrentTransaction(out var transaction))
            {
                throw new NotSupportedException();
            }
            var typedContainer = (TransactionContainer)transaction;

            var counter = Interlocked.Increment(ref _savePointNameCounter);
            var name = $"_auto.{counter}";

            await typedContainer.Transaction.CreateSavepointAsync(name, cancellationToken);
            return new SavepointContainer(this, name);
        }

        #endregion

        //public async ValueTask SaveChangesAsync(CancellationToken cancellationToken)
        //{
        //    if (IsReadOnlyTransaction)
        //    {
        //        throw new Exception();
        //    }
        //    await _dbContext.SaveChangesAsync(cancellationToken);
        //}

        #endregion        

        public async ValueTask DisposeAsync()
        {
            if (TryGetCurrentTransaction(out var transaction))
            {
                await transaction.DisposeAsync();
            }
        }

        #region types

        private class TransactionContainer
            : ITransactionManager.ITransactionContainer
        {
            private readonly EFTransactionManager _transactionManager;

            private bool IsUsed { get; set; }
            private bool IsDisposed { get; set; }

            public IDbContextTransaction Transaction { get; }

            public readonly Dictionary<string, List<(object state, Func<object, CancellationToken, ValueTask> commit, Func<object, CancellationToken, ValueTask> roolback)>> _afterCommitHandlers;

            public TransactionContainer(
                EFTransactionManager unitOfWork,
                IDbContextTransaction transaction)
            {
                _transactionManager = unitOfWork;
                Transaction = transaction;
                IsUsed = false;
                IsDisposed = false;
                _afterCommitHandlers = new Dictionary<string, List<(object, Func<object, CancellationToken, ValueTask>, Func<object, CancellationToken, ValueTask>)>>(2);
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
                    await elem.commit(elem.state, cancellationToken);
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
                    await elem.roolback(elem.state, cancellationToken);
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
                object state,
                Func<object, CancellationToken, ValueTask> commitHandler,
                Func<object, CancellationToken, ValueTask> rolbackHandler)
            {
                var key = _transactionManager.CurrentSavepointContainer?.Name ?? "-1";

                if (!_afterCommitHandlers.TryGetValue(key, out var savepointCollection))
                {
                    savepointCollection = new List<(object, Func<object, CancellationToken, ValueTask>, Func<object, CancellationToken, ValueTask>)>();
                    _afterCommitHandlers.Add(key, savepointCollection);
                }

                savepointCollection.Add((state, commitHandler, rolbackHandler));
            }
        }

        private class SavepointContainer
            : ITransactionManager.ISavepointContainer
        {        
            private readonly EFTransactionManager _transactionManager;
            private readonly TransactionContainer _transactionContainer;
            private readonly SavepointContainer? _prevSavepoint;

            public string Name { get; }
            private bool NoAutoRollBack { get; set; }
            private bool IsDisposed { get; set; }

            public SavepointContainer(                
                EFTransactionManager transactionManager,
                string savePointName
                )
            {
                _transactionManager = transactionManager;                

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
                await _transactionContainer.Transaction.RollbackToSavepointAsync(Name, cancellationToken);

                // Если savepoint откатывается, то созданные в нем хендлеры не будут вызыватся.

                if (_transactionContainer._afterCommitHandlers.TryGetValue(Name, out var savepointCollection))
                {
                    foreach (var elem in savepointCollection)
                    {
                        await elem.roolback(elem.state, cancellationToken);
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

                await _transactionContainer.Transaction.ReleaseSavepointAsync(Name);
                IsDisposed = true;

                _transactionManager.CurrentSavepointContainer = _prevSavepoint;
            }
        }

        #endregion
    }
}
