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

        public string? Name { get; set; }

        public string? Description { get; set; }

        public bool ActivatedOnStart { get; set; }

        public string[] CanRunAction { get; set; }
            = Array.Empty<string>();

        [Obsolete]
        public BaseTokenAction() 
        {
            Id = default!;
        }

        public BaseTokenAction(
            string id,
            string? name)
        { 
            Id = id;
            Name = name;
            ActivatedOnStart = true;
        }
    }
}
