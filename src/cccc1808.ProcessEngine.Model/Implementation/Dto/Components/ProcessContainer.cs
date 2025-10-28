using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;

namespace cccc1808.ProcessEngine.Model.Implementation.Dto.Components
{
    public class ProcessContainer<TId> 
        : IProcessContainer<TId>
    {
        private readonly Dictionary<Type, object> _components 
            = new Dictionary<Type, object>(10);

        public TId Id => Process.Info.Id.Id;
        public IProcessComponent<TId> Process { get; }
        public ICurrentSessionComponent CurrentSession { get; }
        

        public ProcessContainer(
            IProcessComponent<TId> process, 
            ICurrentSessionComponent currentSession)
        {
            Process = process;            
            CurrentSession = currentSession;
            AddComponent(process);
            AddComponent(currentSession);
        }

        public T GetComponent<T>()
        {
            return (T)_components[typeof(T)];
        }

        public void AddComponent<T>(T component)
        {
            _components.Add(typeof(T), component!);

            if (component is IDataComponent dataComponent 
                && typeof(T) != typeof(IDataComponent))
            {
                _components.Add(typeof(IDataComponent), dataComponent);
            }
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

            // TODO: remove data component.
        }
    }
}
