using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Service
{
    public interface ISchemaValidator
    {
        void Validate(
            ProcessTypeDto processType,
            ProcessSchemaDto schema,
            bool needComplete = true);
    }
}
