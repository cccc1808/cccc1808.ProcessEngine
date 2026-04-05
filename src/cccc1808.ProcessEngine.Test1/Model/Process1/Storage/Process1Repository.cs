using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.ProcessModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessModule.Storage;

namespace cccc1808.ProcessEngine.Test1.Model.Process1.Storage
{
    internal class Process1Repository
        : EFChangeTrackerProcessRepository<Guid, ProcessDbEntity<Guid>>
    {
        public Process1Repository(
            IEFDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IProcessRegistry processRegistry,
            IEnumerable<IProcessDbProvider<Guid>> processLoaders,

            IProcessDbEntityConditions<Guid, ProcessDbEntity<Guid>> processDbEntityConditions,
            IProcessErrorDbEntityConditions<Guid> processErrorDbEntityConditions) 
            : base(                  
                  dbContext,                  
                  lockQueryHintStore,      
                  processRegistry,
                  processLoaders,                 
                  processDbEntityConditions,
                  processErrorDbEntityConditions
                  )
        {
        }

        public async Task<Process1DataDbEntity> CreateAsync(
            int version,
            short priority,
            CancellationToken cancellationToken) 
        {
            var id = Guid.NewGuid();

            var process = new Process1DataDbEntity()
            {
                Id = id,
                ProcessId = id,
                States = Process1DataDbEntity.StatesEnum._1,
                Process = new ProcessDbEntity<Guid>()
                {
                    Id = id,
                    Status = ProcessStatusEnum.AsyncExecute,
                    ProcessVersion = version,
                    ProcessTypeId = 0,
                    Priority = priority,
                    Error = new ProcessErrorDbEntity<Guid>()
                    {
                        Id = id
                    },
                    StoppedByError = false,
                    RetryCount = null,
                    SelectLockTimeout = DateTimeOffset.MinValue.UtcDateTime,
                }
            };

            _dbContext.DbContext.Add(process);
            return process;
        }
    }
}
