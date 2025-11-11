using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    /// <summary>
    /// Сущность, связанная с процессом.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IProcessLinkedDbEntity<TId>
    {
        TId ProcessId { get; }
    }
}
