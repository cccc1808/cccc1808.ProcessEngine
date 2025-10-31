using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.Storage;

using Microsoft.EntityFrameworkCore;

using static cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.ChangeTrackerSnapshotService;
using static cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage.ChangeTrackerSnapshotService.DbContextSnapshot;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage
{
    public static class ChangeTrackerSnapshotService 
    {
        #region Types        

        public readonly record struct DbContextSnapshot
        {
            public IReadOnlyList<EntitySnapshot> Entities { get; init; }
            public IReadOnlyDictionary<Type, List<int>> TypeIndex { get; init; }


            public static DbContextSnapshot EmptyConst { get; }
                = new DbContextSnapshot()
                {
                    TypeIndex = ImmutableDictionary<Type, List<int>>.Empty,
                    Entities = Array.Empty<EntitySnapshot>(),
                };

            #region Types

            /// <summary>
            /// Снимок сущности
            /// </summary>
            public readonly record struct EntitySnapshot
            {
                public object Entity { get; init; }
                public EntityState State { get; init; }
                public IReadOnlyDictionary<string, PropertySnapshot> Properties { get; init; }
            }

            /// <summary>
            /// Снимок свойства
            /// </summary>
            public readonly record struct PropertySnapshot
            {
                public object? CurrentValue { get; init; }
                public object? OriginalValue { get; init; }
            }

            #endregion
        }

        #endregion
    }

    public class ChangeTrackerSnapshotService<TDbContext>
        : IChangeTrackerSnapshotService
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        public ChangeTrackerSnapshotService(
            TDbContext dbContext
            )
        {
            _dbContext = dbContext;
        }


        #region public

        public IChangeTrackerSnapshotService.ISubscribe CaptureState()
        {
            var trackerEntities = _dbContext.ChangeTracker.Entries()
                .ToArray();

            if (trackerEntities.Length == 0)
            {
                return new Subscribe(this, EmptyConst);
            }

            var entities = new List<EntitySnapshot>(trackerEntities.Length);
            var typeIndex = new Dictionary<Type, List<int>>();

            for (int i = 0; i < trackerEntities.Length; i++)
            {
                var elem = trackerEntities[i];

                // Сущность.
                {
                    var item = new EntitySnapshot()
                    {
                        Entity = elem.Entity,
                        State = elem.State,
                        Properties = elem.Properties.ToDictionary(
                            e => e.Metadata.Name,
                            e => new PropertySnapshot()
                            {
                                CurrentValue = e.CurrentValue,
                                OriginalValue = e.OriginalValue
                            }
                            ),
                    };
                    entities.Add(item);
                }

                // Индекс типов.
                {
                    if (!typeIndex.TryGetValue(elem.Metadata.ClrType, out var typeIndexStore))
                    {
                        typeIndexStore = new List<int>();
                        typeIndex.Add(elem.Metadata.ClrType, typeIndexStore);
                    }
                    typeIndexStore.Add(i);
                }
            }

            var snapshot = new DbContextSnapshot()
            {
                Entities = entities,
                TypeIndex = typeIndex,
            };

            return new Subscribe(this, snapshot);
        }        

        #endregion

        #region restore

        private void RestoreState(
            in DbContextSnapshot snapshot
            )
        {
            _dbContext.ChangeTracker.Clear();

            if (snapshot.Entities.Count == 0)
            {
                return;
            }
            
            // 1) Отчистка навигационных коллекций в сущностях (не отчищаются автоматически).
            foreach (var elem in snapshot.TypeIndex)
            {
                var clearCollectionAction = GetClearAction(elem.Key);

                if (clearCollectionAction.needClear)
                {
                    foreach (var elem2 in elem.Value)
                    {
                        var entitySnapshot = snapshot.Entities[elem2];
                        clearCollectionAction.clearAction(entitySnapshot.Entity);
                    }
                }
            }

            // 2) Восстанавливаем состояние
            for (int i = 0; i < snapshot.Entities.Count; i++)
            {
                var elem = snapshot.Entities[i];

                // 2.1) Присоединяем
                var entry = _dbContext.Entry(elem.Entity);

                //if (!currentData.TryGetValue(i, out var currentDataElem))
                {
                    // 2.2.1) Заполняем откатываемую сущность (фреймворк дозаполняет навигационные свойства)
                    entry.State = elem.State;
                    foreach (var elem2 in elem.Properties)
                    {
                        var prop = entry.Property(elem2.Key);
                        prop.OriginalValue = elem2.Value.OriginalValue;
                        prop.CurrentValue = elem2.Value.CurrentValue;
                    }
                }
            }            
        }

        private static (bool needClear, Action<object> clearAction) GetClearAction(
            Type type
            )
        {
            var clearAction = _clearCollectionActions.GetOrAdd(
                type,
                valueFactory: static (type) =>
                {
                    static void BuildClearDelegateIfNeed(
                        Type type,
                        PropertyInfo elem,
                        List<Action<object>> clearActionsBuffer
                        )
                    {
                        var genericType = typeof(ICollection<>);
                        Type collectionItemType = null!;

                        var isCollection =
                            elem.PropertyType.IsClass
                            && (
                                //Предполагаем, что коллекация мутабельная, и интерфейс используется, чтобы в нее не писали напрямую,
                                //но выполнить каст к ICollection можно.
                                TypeHelper.TryGetGenericInterfaceParameter(elem.PropertyType, genericType, 0, out collectionItemType)
                                );

                        if (isCollection)
                        {
                            var typedCollectionType = genericType.MakeGenericType(collectionItemType);

                            var method = typedCollectionType
                                .GetMethod(nameof(ICollection<object>.Clear));

                            var objectParameter = Expression.Parameter(typeof(object));
                            var callExpression =
                                // ICollection<TProperty> property -> void .Clear()
                                Expression.Call(
                                    // Object property -> ICollection<TProperty> property
                                    instance: Expression.Convert(
                                        // TEntity entity -> Object .GetProperty()
                                        expression: Expression.Property(
                                            // Object entity -> TEntity entity
                                            expression: Expression.Convert(
                                                expression: objectParameter,
                                                type: type
                                                ),
                                            property: elem
                                            ),
                                        type: typedCollectionType
                                        ),
                                    method: method!
                                    );

                            var clearAction = Expression
                                .Lambda<Action<object>>(callExpression, objectParameter)
                                .Compile();

                            clearActionsBuffer.Add(clearAction);
                        }
                    }

                    var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                    List<Action<object>> clearActions = new(properties.Length);

                    foreach (var elem in properties)
                    {
                        BuildClearDelegateIfNeed(type, elem, clearActions);
                    }

                    if (clearActions.Count == 0)
                    {
                        return (false, (e) => { });
                    }
                    else
                    {
                        clearActions.TrimExcess();
                        return (
                            true,
                            (e) =>
                            {
                                foreach (var elem in clearActions)
                                {
                                    elem(e);
                                }
                            }
                        );
                    }
                }
                );
            return clearAction;
        }

        private static readonly ConcurrentDictionary<Type, (bool needClear, Action<object>)> _clearCollectionActions
            = new();

        #endregion


        #region types

        protected record Subscribe
            : IChangeTrackerSnapshotService.ISubscribe
        {
            private readonly ChangeTrackerSnapshotService<TDbContext> _captureStateService;
            private readonly DbContextSnapshot _snapshot;
            private bool IsUsed { get; set; }


            public Subscribe(
                ChangeTrackerSnapshotService<TDbContext> captureStateService,
                in DbContextSnapshot snapshot
                )
            {
                _captureStateService = captureStateService;
                _snapshot = snapshot;
                IsUsed = false;
            }


            public void NoRestore()
            {
                if (IsUsed)
                {
                    return;
                }

                SetUsed();
            }

            public void Restore()
            {
                if (IsUsed)
                {
                    return;
                }

                _captureStateService.RestoreState(_snapshot);
                SetUsed();
            }

            public void Dispose()
            {
                if (IsUsed)
                {
                    return;
                }

                Restore();
            }

            private void SetUsed()
            {
                IsUsed = true;

                //Уменьшаем нагрузку на GC, очищаем коллекции

                {
                    var typedTypeIndex = (Dictionary<Type, List<int>>)_snapshot.TypeIndex;

                    foreach (var elem in typedTypeIndex)
                    {
                        foreach (var elem2 in elem.Value)
                        {
                            var item = _snapshot.Entities[elem2];
                            var propertyCollection = (Dictionary<string, PropertySnapshot>)item.Properties;

                            propertyCollection.Clear();
                            propertyCollection.TrimExcess();
                        }
                    }

                    typedTypeIndex.Clear();
                    typedTypeIndex.TrimExcess();
                }

                {
                    var typedEntities = (List<EntitySnapshot>)_snapshot.Entities;
                    typedEntities.Clear();
                    typedEntities.TrimExcess();
                }
            }
        }

        #endregion
    }
}