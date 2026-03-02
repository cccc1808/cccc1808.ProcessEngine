using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;

namespace cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute
{
    /// <summary>
    /// Базовая реализация процессов по одному.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public abstract class BaseSingleProcessHandler<TId> 
        : BaseRangeProcessHandler<TId>
    {
        private readonly IIsolationService _isolationService;

        protected BaseSingleProcessHandler(
            IIsolationService isolationService, 
            IProcessRepository<TId> repository, 
            IProcessSetter processSetter)
            : base(
                  repository,
                  processSetter)
        {
            _isolationService = isolationService;
        }

        #region ExecuteStepByStepGroupMiddleware<TId>.IHandler

        public override ExecuteStepByStepGroupMiddleware<TId>.OptionsDto Options => SingleOptions.GroupOptions;

        public override async ValueTask StepRangeAsync(
            ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup group, 
            CancellationToken cancellationToken)
        {
            var options = SingleOptions;

            // Делаем по одному шагу для каждого экземпляра процесса.
            foreach (var elem in group.Group.Values)
            {
                var elemGroup = new ExecuteStepByStepGroupMiddleware<TId>.ExecuteGroup(
                    new Dictionary<ProcessIdDto<TId>, IProcessContainer<TId>>() { [elem.Process.Info.Id] = elem }
                    );

                await _isolationService.ExecuteAsync(
                    SingleOptions.ProcessIsolation,
                    (This: this, elem, elemGroup, options),
                    static async (p, cancellationToken) =>
                    {
                        await p.This.StepAsync(
                            p.elem,
                            cancellationToken);

                        if (p.options.UseSave)
                        {
                            await p.This.SaveRangeAsync(
                                p.elemGroup,
                                cancellationToken);
                        }
                    },
                    static async (p, ex, cancellationToken) =>
                    {
                        await p.This.OnExceptionRangeAsync(
                            p.elemGroup,
                            ex,
                            cancellationToken);
                    },
                    null,
                    cancellationToken
                    );
            }
        }

        #endregion

        protected abstract OptionsDto SingleOptions { get; }

        protected abstract ValueTask StepAsync(
            IProcessContainer<TId> process,
            CancellationToken cancellationToken);

        public record OptionsDto(
            ExecuteStepByStepGroupMiddleware<TId>.OptionsDto GroupOptions,
            IIsolationService.IsolationMode ProcessIsolation,
            bool UseSave
            );
    }
}
