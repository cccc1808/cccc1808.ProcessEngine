using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Storage;

namespace cccc1808.ProcessEngine.Model.Abstract.Services
{
    /// <summary>
    /// Частный слючай для EF.ChangeTracker
    /// </summary>
    public interface IChangeTrackerCompensateService
        : ICompensateService
    {
    }
}
