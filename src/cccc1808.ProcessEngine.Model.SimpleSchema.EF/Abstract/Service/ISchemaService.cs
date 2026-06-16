using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service
{
    public interface ISchemaService<TId>
    {
        /// <summary>
        /// Получить хендлер процесса.
        /// </summary>
        /// <param name="processType"></param>
        /// <returns></returns>
        ISchemaProcessHandler<TId> GetProcessHandler(
            ProcessTypeDto processType);

        ValueTask<string> GetSchemaStartTokenId(
            ProcessTypeDto processType,
            CancellationToken cancellationToken);

        /// <summary>
        /// Получить токен схемы.
        /// </summary>
        /// <param name="processType"></param>
        /// <param name="tokenId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask<TokenDto> GetSchemaToken(
            ProcessTypeDto processType,
            string tokenId,
            CancellationToken cancellationToken);
    }
}
