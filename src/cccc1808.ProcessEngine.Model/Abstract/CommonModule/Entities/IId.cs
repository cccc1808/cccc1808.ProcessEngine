using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities
{
    public interface IId<TId>
    {
        TId Id { get; set; }
    }
}
