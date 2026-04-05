using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test1.Model.Process1.Storage
{
    internal class Process1DataDbProvider
        : IProcessDbProvider<Guid>
    {
        private readonly AppDbContext _dbContext;
        private readonly Id_RangeCondition<Guid, Process1DataDbEntity> id_RangeCondition;

        public Process1DataDbProvider(
            AppDbContext dbContext
            )
        {
            _dbContext = dbContext;

            id_RangeCondition = new Id_RangeCondition<Guid, Process1DataDbEntity>();           
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            var keys = byTypeIndex[new ProcessTypeDto(0, 0)];
            var process1 = keys.Select(e => processes[e]);

            var data = await _dbContext.Process1Datas
                .ApplayQueryCondition(
                    id_RangeCondition,
                    keys
                )
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            foreach (var elem in process1)
            {
                elem.AddComponent(data[elem.Id]);
            }
        }

        public Task LoadRangeAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex, 
            bool withLock,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException("[Bug.]");
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<Guid>> processes,
            IDictionary<ProcessTypeDto, ICollection<Guid>> byTypeIndex,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
