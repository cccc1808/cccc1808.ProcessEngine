using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Services
{
    public interface IProcessCleaner
    {
        Task CleanAsync(CancellationToken cancellationToken);
    }
}
