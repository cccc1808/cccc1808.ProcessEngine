using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto
{
    public record TokenDto
    {
        public string Id { get; }

        public string? Name { get; init; }

        public ITokenAction[] Actions { get; }

        public TokenDto(
            string id,
            params ITokenAction[] actions)
        {
            Id = id;
            Actions = actions;
        }

        public ITokenAction GetAction(string id) 
        {
            var action = Actions.FirstOrDefault(x => x.Id == id) 
                ?? throw new KeyNotFoundException($"Действие не найдено {id}");

            return action;
        }
    }
}
