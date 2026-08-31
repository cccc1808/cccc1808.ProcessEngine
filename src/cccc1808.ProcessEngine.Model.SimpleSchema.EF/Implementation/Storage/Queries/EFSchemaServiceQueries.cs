using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Storage.Queries
{
    public class EFSchemaServiceQueries<TId>
        : SchemaService<TId>.IQueries
    {
        private readonly IEFDbContext _dbContext;

        public EFSchemaServiceQueries(
            IEFDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<JsonElement> GetSchemaAsync(
            ProcessTypeDto processType, 
            CancellationToken cancellationToken)
        {
            var schema = await _dbContext.Set<SchemaDbEntity<TId>>()
                .Where(e => 
                    e.ProcessTypeId == processType.ProcessType 
                    && e.ProcessVersion == processType.ProcessVersion)
                .Select(e => e.Schema)
                .FirstAsync();

            return schema;
        }
    }
}
