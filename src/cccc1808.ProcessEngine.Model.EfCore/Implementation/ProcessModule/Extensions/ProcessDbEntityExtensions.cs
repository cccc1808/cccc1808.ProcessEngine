using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Extensions
{
    /// <summary>
    /// TODO: move to setter.
    /// </summary>
    public static class ProcessDbEntityExtensions
    {        
        public static ProcessTypeUniqueDto ToProcessTypeUnique<TId, TEntity>(
            this TEntity process)
            where TEntity : ProcessDbEntity<TId>
        {
            return new ProcessTypeUniqueDto(
                new ProcessTypeDto(process.ProcessTypeId, process.ProcessVersion), 
                process.Priority);
        }
    }
}
