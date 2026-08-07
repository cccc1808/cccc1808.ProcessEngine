using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto
{
    /// <summary>
    /// Контенер данныз о идентефикаторе и типе процесса.
    /// </summary>
    public readonly record struct ProcessInstanceInfoDto<TId>(
        TId Id,
        ProcessRegistryDto Registry)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(
                Registry.Unique.GetHashCode(),
                Id);
        }
    }
}
