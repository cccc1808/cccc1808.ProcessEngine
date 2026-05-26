using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation
{
    public interface ICompensateService 
    {        
        public interface ICompensateScope
            : IAsyncDisposable
        {
            /// <summary>
            /// Зарегистрировать ручной хендлер компенсации.
            /// </summary>
            /// <param name="manualCompensateHandler"></param>
            void RegisterManualCompensateHandler(
                Func<CancellationToken, ValueTask> manualCompensateHandler);

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

        public class CompoundScope : ICompensateScope
        {
            private readonly IEnumerable<ICompensateScope> _scopes;

            public CompoundScope(
                IEnumerable<ICompensateScope> scopes)
            {
                _scopes = scopes;
            }

            public void RegisterManualCompensateHandler(
                Func<CancellationToken, ValueTask> manualCompensateHandler)
            {
                _scopes.First().RegisterManualCompensateHandler(manualCompensateHandler);
            }

            public async ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                foreach (var elem in _scopes) 
                {
                    await elem.CommitAsync(cancellationToken);
                }
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                foreach (var elem in _scopes)
                {
                    await elem.CompensateAsync(cancellationToken);
                }
            }

            public async ValueTask DisposeAsync()
            {
                foreach (var elem in _scopes)
                {
                    await elem.DisposeAsync();
                }
            }            
        }
    }

    /// <summary>
    /// Система коменсации изменений.
    /// </summary>
    public interface ICompensateService<TId> : ICompensateService
    {
        /// <summary>
        /// Начать отслживание изменений.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<ICompensateScope> StartScopeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken);        
    }
}
