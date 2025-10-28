using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components.Conditions;
using cccc1808.ProcessEngine.Model.Implementation.Storage;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test1.Model.Process1.Storage
{
    internal class Process1DataDbProvider
        : IProcessDbProvider<Guid>
    {
        private readonly AppDbContext _dbContext;
        private readonly IId_RangeCondition<Guid, Process1DataDbEntity> id_RangeCondition;
        private readonly IProcessContainer_Id_Condition<Guid> processEntity_Id_Condition;

        public Process1DataDbProvider(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            id_RangeCondition = new IId_RangeCondition<Guid, Process1DataDbEntity>();
            processEntity_Id_Condition = new IProcessContainer_Id_Condition<Guid>();
        }

        public async Task LoadForAsyncProcessingAsync(
            IDictionary<Guid, IProcessContainer<Guid>> processes, 
            CancellationToken cancellationToken)
        {
            var process1 = processes.Values
                .Where(e => e.Process.Info.ProcessType.ProcessType == 0)
                .ToArray();

            var data = await _dbContext.Process1Datas.ApplayFilterCondition(
                id_RangeCondition,
                process1.ApplayProjectionCondition(processEntity_Id_Condition).ToArray()
                )
                .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

            foreach (var elem in process1)
            {
                elem.AddComponent(data[elem.Id]);
            }
        }

        public Task UpdateAsync(
            ICollection<IProcessContainer<Guid>> processes,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
