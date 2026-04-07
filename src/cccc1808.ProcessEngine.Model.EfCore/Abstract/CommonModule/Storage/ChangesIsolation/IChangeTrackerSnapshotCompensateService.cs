using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Создать снимок восстановления для текущего состояния.
    /// TODO: возможно передавать сюда список процессов, чтобы была возможность также захватить состоянеи InMemory состояния
    /// например <see cref="IAsyncSessionComponent"/>, <see cref="IProcessComponent.Error"/>.    /// 
    /// Хотя этот ICompensateService скорее эксперементальный.
    /// </summary>
    public interface IChangeTrackerSnapshotCompensateService
        : ICompensateService
    {
    }
}
