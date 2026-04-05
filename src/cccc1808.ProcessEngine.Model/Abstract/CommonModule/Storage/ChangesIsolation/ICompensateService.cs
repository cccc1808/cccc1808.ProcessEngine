using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Система коменсации изменений.
    /// </summary>
    public interface ICompensateService
    {
        /// <summary>
        /// Начать отслживание изменений.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<ICompensateScope> StartScopeAsync(CancellationToken cancellationToken);

        public interface ICompensateScope
            : IAsyncDisposable
        {
            /// <summary>
            /// Подвердить изменения.
            /// </summary>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            ValueTask CommitAsync(CancellationToken cancellationToken);

            /// <summary>
            /// Сбросить изменения.
            /// </summary>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            ValueTask CompensateAsync(CancellationToken cancellationToken);
        }
    }
}
