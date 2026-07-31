using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos;

namespace cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Services
{
    public interface IStaticInstanceRegistry
    {
        short GetDeployVersion();

        IReadOnlySet<StaticInstanceProcessRegistrationDto> All();
    }
}
