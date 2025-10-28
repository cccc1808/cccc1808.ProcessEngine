using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Storage;

using Microsoft.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore.Storage;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage
{
    public class EFTransactionManager<TDbContext>
        : ITransactionManager
        where TDbContext : DbContext
    {
        protected readonly TDbContext _dbContext;

        private TransactionContainer? CurrentTransactionContainer { get; set; }
        protected bool IsReadOnlyTransaction { get; set; }

        private int _savePointNameCounter;


        public EFTransactionManager(
            TDbContext dbContext)
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

            var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
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
            return new SavepointContainer(typedContainer.Transaction, name);
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
            private readonly EFTransactionManager<TDbContext> _unitOfWork;

            private bool IsUsed { get; set; }
            private bool IsDisposed { get; set; }

            public IDbContextTransaction Transaction { get; }

            public TransactionContainer(
                EFTransactionManager<TDbContext> unitOfWork,
                IDbContextTransaction transaction)
            {
                _unitOfWork = unitOfWork;
                Transaction = transaction;
                IsUsed = false;
                IsDisposed = false;
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
            }

            public async Task RollbackAsync(CancellationToken cancellationToken)
            {
                if (IsUsed)
                {
                    throw new NotSupportedException("Действие уже выоплнено, некорректное использование scope.");
                }

                await Transaction.RollbackAsync(cancellationToken);
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
                _unitOfWork.CurrentTransactionContainer = null;
                _unitOfWork.IsReadOnlyTransaction = false;

                IsDisposed = true;
            }
        }

        private class SavepointContainer
            : ITransactionManager.ISavepointContainer
        {
            private readonly string _savePointName;
            private readonly IDbContextTransaction _transaction;

            private bool NoAutoRollBack { get; set; }
            private bool IsDisposed { get; set; }



            public SavepointContainer(
                IDbContextTransaction transaction,
                string savePointName)
            {
                _transaction = transaction;
                _savePointName = savePointName;
                NoAutoRollBack = false;
                IsDisposed = false;
            }

            public void NoAutoRollback()
            {
                NoAutoRollBack = true;
            }

            public async Task RollbackAsync(CancellationToken cancellationToken)
            {
                await _transaction.RollbackToSavepointAsync(_savePointName, cancellationToken);

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

                await _transaction.ReleaseSavepointAsync(_savePointName);
                IsDisposed = true;
            }
        }

        #endregion
    }
}
