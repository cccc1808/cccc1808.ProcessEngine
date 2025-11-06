using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Batch
{
    /// <summary>
    /// Хранилище компонентов, общих для батча процессов.
    /// (Этот компонент можно получить у любого экземпляра процесса).
    /// </summary>
    public interface IBatchComponentContainer
    {
        void AddComponent<T>(T component);

        T GetComponent<T>();

        bool TryGetComponent<T>(out T result);
    }
}
