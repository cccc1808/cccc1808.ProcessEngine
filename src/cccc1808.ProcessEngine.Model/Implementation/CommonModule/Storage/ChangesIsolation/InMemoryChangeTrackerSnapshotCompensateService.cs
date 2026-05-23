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
    /// <see cref="IInmemoryMutableState"/>.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class InMemoryChangeTrackerSnapshotCompensateService<TId> 
        : IChangeTrackerSnapshotCompensateService<TId>
    {
        public virtual ValueTask<ICompensateService.ICompensateScope> StartScopeAsync(
            IDictionary<TId, IProcessContainer<TId>> processes, 
            CancellationToken cancellationToken)
        {
            // Создаем снимок процессов и компонентов.
            var processState = new Dictionary<TId, EntryDto>(processes.Count);

            foreach (var elem in processes.Values)
            {
                var components = elem.AllComponents;
                var entry = new EntryDto(elem, components.Count);                

                foreach (var elem2 in components)
                {
                    // 1) Записываем данные о наличие компонента.
                    entry.Components.Add(elem2.Key, elem2);

                    // 2) Если компонент поддерживаем снимок, то создаем его.
                    if (elem2.Value is IInmemoryMutableState inmemoryMutableState)
                    {
                        var snapshot = inmemoryMutableState.Capture();
                        entry.Snapshots.Add(elem2.Key, snapshot);
                    }
                }

                processState.Add(elem.Id, entry);
            }

            return ValueTask.FromResult<ICompensateService.ICompensateScope>(
                new Scope(
                    processes,
                    processState
                    )
                );
        }

        private readonly record struct EntryDto
        {
            /// <summary>
            /// Процес.
            /// </summary>
            public IProcessContainer<TId> Process { get; }

            /// <summary>
            /// Компоненты процесса.
            /// </summary>
            public Dictionary<Type, object> Components { get; }

            /// <summary>
            /// Снепшоты компонентов процесса.
            /// </summary>
            public Dictionary<Type, IInmemoryMutableState.ISnapshot> Snapshots { get; }

            public EntryDto(
                IProcessContainer<TId> process,
                int componentsCount)
            {
                Process = process;
                Components = new Dictionary<Type, object>(componentsCount);
                Snapshots = new Dictionary<Type, IInmemoryMutableState.ISnapshot>(componentsCount);
            }
        }

        private class Scope : ICompensateService.ICompensateScope
        {
            private bool isCommited;
            private readonly IDictionary<TId, IProcessContainer<TId>> _group;
            private readonly Dictionary<TId, EntryDto> _snapshot;

            public Scope(
                IDictionary<TId, IProcessContainer<TId>> group,
                Dictionary<TId, EntryDto> snapshot)
            {
                _snapshot = snapshot;
                _group = group;
            }

            public ValueTask CommitAsync(CancellationToken cancellationToken)
            {
                ClearMemory();
                isCommited = true;

                return ValueTask.CompletedTask;
            }

            public ValueTask CompensateAsync(CancellationToken cancellationToken)
            {
                static void ProcessComponenets(
                    in EntryDto entry)
                {
                    // Восстанавливаем состояние IProcessContainer на основе снимка.
                    var currentComponenets = entry.Process.AllComponents.ToArray(); // Копия, потому что меняется при перечислении.
                    var snapshotComponents = entry.Components.ToDictionary();

                    foreach (var elem2 in currentComponenets)
                    {
                        if (snapshotComponents.ContainsKey(elem2.Key))
                        {
                            // 1) Компонент был на начало снимка.

                            if (entry.Snapshots.TryGetValue(elem2.Key, out var snapshot))
                            {
                                var inmemoryMutableState = (IInmemoryMutableState)elem2.Value;
                                inmemoryMutableState.Restore(snapshot);
                            }

                            snapshotComponents.Remove(elem2.Key);
                        }
                        else
                        {
                            // 2) Компонент был добавлен, удаляем.
                            entry.Process.RemoveComponent(elem2.Key);
                        }
                    }

                    foreach (var elem2 in snapshotComponents)
                    {
                        // 3) Компонент был удален, восстанавливаем.
                        entry.Process.AddComponent(elem2.Key, elem2.Value);

                        if (entry.Snapshots.TryGetValue(elem2.Key, out var snapshot))
                        {
                            var inmemoryMutableState = (IInmemoryMutableState)elem2.Value;
                            inmemoryMutableState.Restore(snapshot);
                        }
                    }
                }

                // Восстанавливаем состояние группы процессов на основе снимка.
                var currentGroup = _group.ToArray(); // Копия, потому что меняется при перечислении.
                var snapshotGroup = _snapshot.ToDictionary();
    
                foreach (var elem in currentGroup)
                {
                    if (_snapshot.TryGetValue(elem.Key, out var entry))
                    {
                        // 1) Процесс был на начало снимка.

                        ProcessComponenets(entry);

                        snapshotGroup.Remove(elem.Key);
                    }
                    else 
                    {
                        // 2) Процесс был добавлен, удаляем.
                        _group.Remove(elem.Key);
                    }
                }

                foreach (var elem2 in snapshotGroup)
                {
                    // 3) Процесс был удален, восстанавливаем.

                    _group.Add(elem2.Key, elem2.Value.Process);

                    ProcessComponenets(elem2.Value);
                }

                return ValueTask.CompletedTask;
            }

            public async ValueTask DisposeAsync()
            {
                if (isCommited)
                {
                    return;
                }

                await CompensateAsync(CancellationToken.None);

                ClearMemory();
            }

            private void ClearMemory() 
            {
                foreach (var elem in _snapshot.Values)
                {
                    foreach (var elem2 in elem.Snapshots.Values)
                    {
                        elem2.Dispose();
                    }

                    elem.Components.Clear();
                    elem.Snapshots.Clear();
                }

                _snapshot.Clear();
            }
        }
    }
}
