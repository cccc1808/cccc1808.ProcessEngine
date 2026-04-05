using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage.ChangesIsolation;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Services;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.Implementation.ProcessExecutionModule.Services.ProcessExecuteMiddlewares.Execute;
using cccc1808.ProcessEngine.Test1.Model.Process1.Storage;

namespace cccc1808.ProcessEngine.Test1.Model.Process1
{
    internal class Handler1
        : BaseSingleProcessHandler<Guid>
    {
        public Handler1(
            AppDbContext dbContext,
            IIsolationService isolationService,
            Process1Repository repository,
            ITriggerRepository<Guid> triggerRepository,
            IProcessSetter processSetter
            )
            : base(
                  isolationService,                  
                  repository,
                  triggerRepository,
                  processSetter)
        {
        }


        protected override OptionsDto SingleOptions => Presets<Guid>.Preset2_Single;

        protected override ValueTask StepAsync(
            IProcessContainer<Guid> process,
            CancellationToken cancellationToken)
        {
            if (!process.TryGetComponent<Process1DataDbEntity>(out var typedProcess))
            {
                throw new ArgumentException();
            }

            switch (typedProcess.States)
            {
                case Process1DataDbEntity.StatesEnum._1:
                    {
                        typedProcess.Counter++;
                        typedProcess.States = Process1DataDbEntity.StatesEnum._2;
                        break;
                    }

                case Process1DataDbEntity.StatesEnum._2:
                    {
                        typedProcess.Counter++;
                        typedProcess.States = Process1DataDbEntity.StatesEnum._3;
                        _processSetter.SetStatus(process, ProcessStatusEnum.Complete);

                        break;
                    }
            }

            return ValueTask.CompletedTask;
        }
    }
}
