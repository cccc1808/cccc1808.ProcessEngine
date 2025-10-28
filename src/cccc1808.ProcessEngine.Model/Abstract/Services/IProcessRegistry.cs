using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;

namespace cccc1808.ProcessEngine.Model.Abstract.Services
{
    public interface IProcessRegistry
    {
        ICollection<ProcessRegistryDto> All();
    }
}
