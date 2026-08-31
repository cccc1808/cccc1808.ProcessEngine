using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions
{
    public class BaseTokenAction : ITokenAction
    {
        public string Id { get; set; }

        public string? Name { get; set; }

        public bool ActivatedOnStart { get; set; }

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

        public override string ToString()
        {
            return $"{Id} | {Name}";
        }
    }
}
