using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto
{
    public record TokenDto
    {
        public string Id { get; }

        public ITokenAction[] Actions { get; }

        public TokenDto(
            string id,
            ITokenAction[] actions)
        {
            Id = id;
            Actions = actions;
        }
    }
}
