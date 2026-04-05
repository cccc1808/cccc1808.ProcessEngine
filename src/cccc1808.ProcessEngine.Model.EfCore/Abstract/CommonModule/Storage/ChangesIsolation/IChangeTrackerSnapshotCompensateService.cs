using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage.ChangesIsolation
{
    public interface IChangeTrackerSnapshotCompensateService
        : ICompensateService
    {
    }
}
