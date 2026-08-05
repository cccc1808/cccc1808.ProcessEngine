using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Ручная система компенсации.
    /// </summary>
    public class NoIsolationCompensateService
        : INoIsolationCompensateService
    {
        private int ScopeIndex { get; set; }
            = IsolationContainer.TransactionIsolationIndex + 1;

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var scope = new Scope(++ScopeIndex);
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(scope);
        }

        private class Scope : ICompensateService.ICompensateScope
        {
            private readonly List<(object state, Func<int, object, CancellationToken, ValueTask> handler)> _manualCompensateHandlers;

            private bool IsCommited { get; set; }

            private bool IsDisposed { get; set; }

            public int ScopeIndex { get; }

            public Scope(int scopeId)
            {
                _manualCompensateHandlers = new List<(object state, Func<int, object, CancellationToken, ValueTask> handler)>(5);
                ScopeIndex = scopeId;
            }

            public void RegisterManualCompensateHandler(
                object state,
                Func<int, object, CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add((state, manualCompensateHandler));
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                IsCommited = true;
                return ValueTask.CompletedTask;
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                //if (IsCommited)
                //{
                //    throw new InvalidOperationException("[Bug]. Компенсация после коммита.");
                //}

                foreach (var elem in _manualCompensateHandlers)
                {
                    await elem.handler(ScopeIndex, elem.state, cancellationToken);
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (!IsDisposed)
                {
                    if (!IsCommited)
                    {
                        await CompensateAsync(CancellationToken.None);
                    }

                    _manualCompensateHandlers.Clear();
                    IsDisposed = true;
                }
            }
        }
    }
}
