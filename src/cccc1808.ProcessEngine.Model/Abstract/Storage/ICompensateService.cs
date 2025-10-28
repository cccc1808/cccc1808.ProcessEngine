using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Storage
{
    public interface ICompensateService
    {
        ValueTask<ICompensateScope> StartScopeAsync(CancellationToken cancellationToken);

        public interface ICompensateScope
            : IAsyncDisposable
        {
            ValueTask CommitAsync(CancellationToken cancellationToken);

            ValueTask CompensateAsync(CancellationToken cancellationToken);
        }
    }

    public interface IManualCompensateService
        : ICompensateService
    {
        /// <summary>
        /// Сохранить хендлер компенсации (после выполнения действия).
        /// </summary>
        void AddCompensate(Func<CancellationToken, ValueTask> compensate);

        /// <summary>
        /// Выполнить действие и после сохранить хендлер компенсации.
        /// </summary>
        ValueTask ExecuteWithCompensate(
            Func<ValueTask> action,
            Func<CancellationToken, ValueTask> compensate);
    }
}
