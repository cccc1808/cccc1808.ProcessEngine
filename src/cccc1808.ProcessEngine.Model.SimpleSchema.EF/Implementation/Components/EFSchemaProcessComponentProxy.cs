using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Components
{
    public class EFSchemaProcessComponentProxy<TId> : ISchemaProcessComponent
    {
        private readonly Dictionary<string, ITokenActionStateComponent> _actionState;

        public SchemaProcessDataDbEntity<TId> Entity { get; }

        public string RootTriggerKey => Entity.RootTriggerKey;

        public string CurrentTokenId { get => Entity.CurrentTokenId; set => Entity.CurrentTokenId = value; }

        public bool AutoDetectStreamTriggers => true;

        public EFSchemaProcessComponentProxy(
            SchemaProcessDataDbEntity<TId> entity,
            ICollection<ITokenActionStateComponent> actionState)
        {
            _actionState = actionState.ToDictionary(e => e.Id, e => e);
            Entity = entity;
        }

        public void AddActionState<TState>(TState state)
            where TState : ITokenActionStateComponent
        {
            _actionState.Add(state.Id, state);
        }

        public bool TryGetActionState<TState>(string name, out TState state)
            where TState : ITokenActionStateComponent
        {
            if (_actionState.TryGetValue(name, out var notTypedState))
            {
                state = (TState)notTypedState;
                return true;
            }

            state = default!;
            return false;
        }

        public void ClearActionStates()
        {
            _actionState.Clear();
        }

        public IReadOnlyCollection<ITokenActionStateComponent> AllActionStates()
        {
            return _actionState.Values;
        }
    }
}
