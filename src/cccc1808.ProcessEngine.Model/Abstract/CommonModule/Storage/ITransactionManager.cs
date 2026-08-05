using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage
{
    public interface ITransactionManager
    {
        Task<ITransactionContainer> StartTransactionAsync(
            CancellationToken cancellationToken);

        bool TryGetCurrentTransaction(
            out ITransactionContainer transaction);

        Task<ISavepointContainer> CreateSavepointAsync(
            CancellationToken cancellationToken);        

        public interface ITransactionContainer
            : IAsyncDisposable
        {
            void NoAction();

            Task CommitAsync(CancellationToken cancellationToken);

            Task RollbackAsync(CancellationToken cancellationToken);

            /// <summary>
            /// Добавить хендлер, вызываемый после успешного коммита транзакции.
            /// (В случае отката транзакции или текущего savepoint, хендлер не будет вызван).
            /// </summary>
            void AddAfterCommitHandler(
                object state,
                Func<object, CancellationToken, ValueTask> commitHandler,
                Func<object, CancellationToken, ValueTask> roolbackHandler);
        }

        public interface ISavepointContainer
            : IAsyncDisposable
        {
            void NoAutoRollback();

            Task RollbackAsync(CancellationToken cancellationToken);
        }
    }
}
