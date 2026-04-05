using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation
{
    /// <summary>
    /// Частный слючай для EF.ChangeTracker
    /// </summary>
    public interface IChangeTrackerCompensateService
        : ICompensateService
    {
    }
}
