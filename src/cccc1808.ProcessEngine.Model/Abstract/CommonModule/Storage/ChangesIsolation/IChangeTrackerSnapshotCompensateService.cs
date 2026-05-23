using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Создание InMemory снимка.
    /// </summary>
    public interface IChangeTrackerSnapshotCompensateService<TId>
        : ICompensateService<TId>
    {
    }
}
