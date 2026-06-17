using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto
{
    public record SchemaProcessRegistrationDto(
        ProcessTypeDto ProcessType,
        Type ProcessHandlerType,
        Type ProcessStateHandlerType)
    {
        public static SchemaProcessRegistrationDto Create<TId, THandler, TStateHadnler>(
            ProcessTypeDto processType)
            where THandler : ISchemaProcessHandler<TId>
            where TStateHadnler : ISchemaProcessStateHandler<TId>
        {
            return new SchemaProcessRegistrationDto(
                processType, 
                typeof(THandler),
                typeof(TStateHadnler));
        }
    }
}
