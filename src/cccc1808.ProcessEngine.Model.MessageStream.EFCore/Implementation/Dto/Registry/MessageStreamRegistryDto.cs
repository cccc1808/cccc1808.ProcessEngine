using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Registry;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Dto.Registry
{
    /// <summary>
    /// Регистрация типов процессов, которые обрабатываются как MessageStream.
    /// </summary>
    /// <param name="Process"></param>
    public record MessageStreamRegistryDto(
        ProcessRegistryDto Process)
    {
    }
}
