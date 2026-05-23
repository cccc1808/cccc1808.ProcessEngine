using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Ручная система компенсации.
    /// Предоставляет разработчику возможность самостоятельно зарегестировать действие компенсации в момент выполнение какого-либо действия.
    /// Основное применение: откат изменений сделанных напрямую в БД, когда используется EF.ChangeTracker и ChangeTrackerSnapshotCompensateService (например когда нужен InsertOnConflict).
    /// </summary>
    public class ManualCompensateService<TId>
        : IManualCompensateService<TId>
    {      
        private Scope? Current { get; set; }

        public ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes,
            CancellationToken cancellationToken)
        {
            var scope = new Scope(this);
            return ValueTask.FromResult<ICompensateService.ICompensateScope>(scope);
        }

        public void AddCompensate(Func<CancellationToken, ValueTask> compensate)
        {
            Current?.AddCompensate(compensate);
        }

        public async ValueTask ExecuteWithCompensate(
            Func<ValueTask> action,
            Func<CancellationToken, ValueTask> compensate)
        {
            await action();
            AddCompensate(compensate);
        }

        private record Scope : ICompensateService.ICompensateScope
        {
            private readonly ManualCompensateService<TId> _manualCompensateService;
            private readonly List<Func<CancellationToken, ValueTask>> _actions 
                = new List<Func<CancellationToken, ValueTask>>();

            private readonly Scope? _parent;
            private readonly List<Scope> _childs 
                = new List<Scope>(10);

            public Scope(ManualCompensateService<TId> manualCompensateService)
            {
                _manualCompensateService = manualCompensateService;

                if (_manualCompensateService.Current != null)
                {
                    _manualCompensateService.Current._childs.Add(this);
                    _parent = _manualCompensateService.Current;
                }

                _manualCompensateService.Current = this;
            }

            private bool IsCommited { get; set; }

            public void AddCompensate(Func<CancellationToken, ValueTask> compensate)
            {
                _actions.Add(compensate);
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                IsCommited = true;
                return ValueTask.CompletedTask;
            }

            public async ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                static async ValueTask CompensateChildRecursiveAsync(
                    Scope scope,
                    CancellationToken cancellationToken) 
                {
                    foreach (var elem in scope._childs)
                    {
                        await CompensateChildRecursiveAsync(elem, cancellationToken);
                    }

                    foreach (var action in scope._actions)
                    {
                        await action(cancellationToken);
                    }
                }

                if (IsCommited)
                {
                    throw new Exception();
                }
                if (_manualCompensateService.Current != this)
                {
                    throw new Exception();
                }

                await CompensateChildRecursiveAsync(this, cancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                if (IsCommited)
                {
                    return;
                }
                if (_manualCompensateService.Current != this)
                {
                    return;
                }

                await CompensateAsync(default);

                _parent?._childs.Remove(this);
                _manualCompensateService.Current = _parent;
                _actions.Clear();
            }
        }
    }
}
