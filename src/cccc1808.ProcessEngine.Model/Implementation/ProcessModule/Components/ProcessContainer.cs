using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;

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

        public bool UsingWakeup { get; }

        public ProcessContainer(
            IProcessComponent<TId> process,
            IAsyncSessionComponent currentSession,
            bool isAsyncExecuting,
            bool usingWakeup)
        {
            Process = process;
            AddComponent(process);

            CurrentSession = currentSession;
            AddComponent(currentSession);
            InAsyncExecuting = isAsyncExecuting;
            UsingWakeup = usingWakeup;
        }

        public T GetComponent<T>()
        {
            return (T)_components[typeof(T)];
        }

        public void AddComponent<T>(T component)
        {
            _components.Add(typeof(T), component!);
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
            _components.Remove(typeof(T));
        }
    }
}
