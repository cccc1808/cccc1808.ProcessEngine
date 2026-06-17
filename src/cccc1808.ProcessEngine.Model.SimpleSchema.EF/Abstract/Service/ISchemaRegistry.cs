using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service
{
    public interface ISchemaRegistry
    {
        bool IsSchemaRegistryProcess(ProcessTypeDto processType);

        bool TryGetSchema(ProcessTypeDto processType, out ProcessSchemaDto schema);

        bool TryStoreSchema(ProcessTypeDto processType, ProcessSchemaDto schema);

        Type GetProcessHandlerType(ProcessTypeDto processType);

        Type GetProcessStateHandlerType(ProcessTypeDto processType);
    }
}
