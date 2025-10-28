using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Storage
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
        }

        public interface ISavepointContainer
            : IAsyncDisposable
        {
            void NoAutoRollback();

            Task RollbackAsync(CancellationToken cancellationToken);
        }
    }
}
