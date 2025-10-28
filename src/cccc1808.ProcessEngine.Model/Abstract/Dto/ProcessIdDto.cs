using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto
{
    public readonly record struct ProcessIdDto<T>(T Id)
    {
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}
