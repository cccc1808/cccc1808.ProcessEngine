using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Registry
{
    public record ProcessRegistryDto(
        ProcessTypeDto ProcessType,
        short Priority)
    {
    }
}
