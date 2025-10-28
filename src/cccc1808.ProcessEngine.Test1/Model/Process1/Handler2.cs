using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services;
using cccc1808.ProcessEngine.Test1.Model.Process1.Storage;

namespace cccc1808.ProcessEngine.Test1.Model.Process1
{
    internal class Handler2
        : BaseEFChangeTrackerExecuteStepByStepGroupMiddlewareHandler2<Guid>
    {
        public Handler2(
            IIsolationService isolationService,
            Process1Repository repository,
            IProcessSetter processSetter
            ) 
            : base(
                  isolationService,
                  repository,
                  processSetter)
        {
        }

        protected override ValueTask<bool> StepAsync(
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

            return ValueTask.FromResult(true);
        }
    }
}
