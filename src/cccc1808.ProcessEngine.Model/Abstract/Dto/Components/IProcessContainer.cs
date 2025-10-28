using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    /// <summary>
    /// Контейнер содержиащие основные данные процесса,
    /// а также может содержать дополнительные данные конкретной реализации процесса.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IProcessContainer<TId>
    {
        TId Id { get; }
        IProcessComponent<TId> Process { get; }
        ICurrentSessionComponent CurrentSession { get; }

        void AddComponent<T>(T component);

        T GetComponent<T>();

        bool TryGetComponent<T>(out T result);

        void RemoveComponent<T>();
    }
}
