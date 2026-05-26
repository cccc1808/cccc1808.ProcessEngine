using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Ручная система компенсации.
    /// </summary>
    public class NoIsolationCompensateService
        : INoIsolationCompensateService
    {      
        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            CancellationToken cancellationToken)
        {
            var scope = new Scope();
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(scope);
        }

        private class Scope : ICompensateService.ICompensateScope
        {
            private readonly List<Func<CancellationToken, ValueTask>> _manualCompensateHandlers;

            private bool IsCommited { get; set; }

            private bool IsDisposed { get; set; }

            public Scope()
            {
                _manualCompensateHandlers = new List<Func<CancellationToken, ValueTask>>(5);
            }

            public void RegisterManualCompensateHandler(
                Func<CancellationToken, ValueTask> manualCompensateHandler)
            {
                _manualCompensateHandlers.Add(manualCompensateHandler);
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
                    await elem(cancellationToken);
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
