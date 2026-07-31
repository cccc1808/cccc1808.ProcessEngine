using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services.Runners
{
    /// <summary>
    /// Раннер процессов.
    /// Резервирует задачи только если сразу готов взять их в работу (есть свободный слот параллельного выполнения).
    /// DbSelector -> Executor.
    /// </summary>
    public interface IParallelLimitProcessRunner
        : IProcessRunner
    {
        /// <summary>
        /// Запросы к хранилищу.
        /// </summary>
        /// <typeparam name="TId"></typeparam>
        public interface ISelectQuery<TId>
        {
            IContextState BuildContext(ISelectOptions selectOptions);

            Task<ICollection<SelectDto>> SelectForProcessingAsync(
                IContextState contextState,
                CancellationToken cancellationToken);


            public interface ISelectOptions
            {

            }

            public interface IContextState
            {
                void SetFreeSlots(int value);
            }

            public readonly record struct SelectDto(
                ProcessInstanceInfoDto<TId> ProcessInstanceInfo,
                bool IsRangeProcess)
            {
            }
        }
    }
}
