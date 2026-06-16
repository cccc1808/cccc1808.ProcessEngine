using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions
{
    public class BaseTokenAction : ITokenAction
    {
        public string Id { get; set; }

        [Obsolete]
        public BaseTokenAction() 
        {
            Id = default!;
        }

        public BaseTokenAction(
            string id)
        { 
            Id = id;
        }
    }
}
