using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Components
{
    //public class SchemaProcessComponent : ISchemaProcessComponent
    //{
    //    private readonly Dictionary<string, ITokenActionStateComponent> _actionState;

    //    public string RootTriggerKey { get; }

    //    public string CurrentTokenId { get; set; }

    //    public bool AutoDetectStreamTriggers => true;

    //    public SchemaProcessComponent(
    //        string rootTriggerKey,
    //        string currentTokenId)
    //    {
    //        _actionState = new Dictionary<string, ITokenActionStateComponent>(5);
    //        RootTriggerKey = rootTriggerKey;
    //        CurrentTokenId = currentTokenId;
    //    }

    //    public void AddActionState<TState>(string name, TState state)
    //        where TState : ITokenActionStateComponent
    //    {
    //        _actionState.Add(name, state);
    //    }

    //    public bool TryGetActionState<TState>(string name, out TState state)
    //        where TState : ITokenActionStateComponent
    //    {
    //        if (_actionState.TryGetValue(name, out var notTypedState)) 
    //        {
    //            state = (TState)notTypedState;
    //            return true;
    //        }

    //        state = default!;
    //        return false;
    //    }

    //    public void ClearActionStates()
    //    {
    //        _actionState.Clear();
    //    }

    //    public IReadOnlyCollection<KeyValuePair<string, ITokenActionStateComponent>> AllActionStates()
    //    {
    //        return _actionState;
    //    }
    //}
}
