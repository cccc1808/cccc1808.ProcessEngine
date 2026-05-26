
using System.Collections.Generic;
using System.ComponentModel;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Dto;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Components
{
    public class ProcessContainer<TId> 
        : IProcessContainer<TId>
    {
        private readonly Dictionary<Type, object> _components 
            = new Dictionary<Type, object>(10);

        public TId Id => Process.Info.Id;

        public IProcessComponent<TId> Process { get; }

        public IAsyncSessionComponent CurrentSession { get; }

        public bool InAsyncExecuting { get; }

        public WakeupStateEnum WakeupState { get; }

        public IReadOnlyDictionary<Type, object> AllComponents 
            => _components;

        public ProcessContainer(
            IProcessComponent<TId> process,
            IAsyncSessionComponent currentSession,
            bool isAsyncExecuting,
            WakeupStateEnum wakeupState)
        {
            Process = process;
            AddComponent(process);

            CurrentSession = currentSession;
            AddComponent(currentSession);
            InAsyncExecuting = isAsyncExecuting;
            WakeupState = wakeupState;
        }

        public T GetComponent<T>()
        {
            return (T)_components[typeof(T)];
        }

        public void AddComponent<T>(T component)
        {
            AddComponent(typeof(T), component!);
        }

        public void AddComponent(Type type, object component)
        {
            _components.Add(type, component!);
        }

        public bool TryGetComponent<T>(out T result)
        {
           var found = _components.TryGetValue(typeof(T), out var r);
            if (!found)
            {
                result = default!;
                return false;
            }

            result = (T)r!;
            return true;
        }

        public void RemoveComponent<T>()
        {
            RemoveComponent(typeof(T));
        }        

        public void RemoveComponent(Type type)
        {
            _components.Remove(type);
        }
    }
}
