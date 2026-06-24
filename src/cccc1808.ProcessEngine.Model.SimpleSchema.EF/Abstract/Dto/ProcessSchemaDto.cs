using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto
{
    /// <summary>
    /// Схема процесса.
    /// </summary>
    public record ProcessSchemaDto
    {
        /// <summary>
        /// Начальный токен (теоретически необязательно).
        /// </summary>
        public string StartTokenId { get; }

        public string? Description { get; init; }

        /// <summary>
        /// Токены схемы процесса.
        /// </summary>
        public IReadOnlyDictionary<string, TokenDto> Tokens { get; }

        public ProcessSchemaDto(
            string startTokenId, 
            IReadOnlyCollection<TokenDto> tokens)
        {
            StartTokenId = startTokenId;
            Tokens = tokens.ToFrozenDictionary(e => e.Id, e => e);
        }
    }
}
