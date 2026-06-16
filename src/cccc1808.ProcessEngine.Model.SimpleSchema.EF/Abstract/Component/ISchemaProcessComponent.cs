using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component.ActionComponent;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component
{
    public interface ISchemaProcessComponent
    {
        bool AutoDetectStreamTriggers { get; }

        string RootTriggerKey { get; }

        string CurrentTokenId { get; set; }


        bool TryGetActionState<TState>(string name, out TState state)
            where TState : ITokenActionStateComponent;

        void AddActionState<TState>(TState state)
            where TState : ITokenActionStateComponent;

        void ClearActionStates();

        IReadOnlyCollection<ITokenActionStateComponent> AllActionStates();
    }
}
