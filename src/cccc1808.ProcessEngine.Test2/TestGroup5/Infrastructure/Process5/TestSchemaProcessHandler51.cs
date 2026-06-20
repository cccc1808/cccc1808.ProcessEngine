using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Entities;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure.Process5
{
    internal class TestSchemaProcessHandler51 : BaseSchemaProcessHandler<Guid>
    {
        public static ProcessSchemaDto Schema { get; }
            = new ProcessSchemaDto(
                "1",
                [
                    new TokenDto(
                        "1",
                        new ServiceTaskTokenAction("1", "1_CreateChildTrigger")
                        {
                            Name = "Триггер дочерних процессов",
                            Description = "1) Создаем триггер для дочерних процессов",
                            ActivatedOnStart = true,
                            CanRunAction = [new ITokenAction.RunActionDeclarationDto("2", "Переходим к созданию дочерних процессов")],
                        },
                        new ServiceTaskTokenAction("2", "1_RunChildProcesses")
                        {
                            Name = "Дочерние процессы",
                            Description = "2) Запускаем дочерние процессы",
                            ActivatedOnStart = false,
                            CanRunAction = [new ITokenAction.RunActionDeclarationDto("3", "Ожидаем завершения дочерних процессов")],
                        },
                        new ConditionTokenAction("3", "1_CheckChildComplete")
                        {
                            Name = "Ожидаем завершения дочерних процессов",
                            ActivatedOnStart = false,
                            Transition = ITokenAction.TransitionDto.Complete(),
                        }
                        )
                    {
                        Name = "Родительский процесс",
                        Description = "Родительский процесс с CoutnerTrigger",
                    },
                ]
                )
            {
            };

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(51, 1);

        private readonly IEFDbContext _dbContext;
        private readonly ITriggerRepository<Guid> _triggerRepository;

        public TestSchemaProcessHandler51(
            IEFDbContext dbContext, 
            ITriggerRepository<Guid> triggerRepository) :
            base()
        {
            _dbContext = dbContext;
            _triggerRepository = triggerRepository;

            RegistryServiceTask("1_CreateChildTrigger", CreateChildTriggerAsync);
            RegistryServiceTask("1_RunChildProcesses", RunChildProcess);
            RegistryConditionTaskCheck("1_CheckChildComplete", CheckChildProcessesCompleteAsync);
        }

        #region handlers

        private async ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult> CreateChildTriggerAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters, 
            CancellationToken cancellationToken)
        {
            var component = parameters.process.GetComponent<ISchemaProcessComponent>();
            var processState = GetProcessState(component);
            var tokenState = GetOrCreateToken1State(component);

            tokenState.TriggerKey = Guid.NewGuid().ToString();

            await _triggerRepository.CreateTriggerAsync(
                ITriggerRepository<Guid>.CreateTriggerDto.CounterTrigger(
                    key: tokenState.TriggerKey, 
                    timerDate: DateTimeOffset.MinValue, 
                    parameters.process.Id, 
                    isRangeTrigger: true, 
                    handlerKey: EFTimerChildTriggerHandler<Guid>.Name,
                    priority: parameters.process.Process.Info.Priority,
                    isActivated: false,
                    counter: processState.ChildProcessCount,
                    isChildTrigger: true), 
                cancellationToken);

            return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                isComplete: true,
                ISchemaProcessHandler.ActivateActionDto.ServiceTask("2"));
        }

        private ISchemaProcessHandler.ExecuteServiceTaskResult RunChildProcess(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var component = parameters.process.GetComponent<ISchemaProcessComponent>();
            var processState = GetProcessState(component);
            var tokenState = GetOrCreateToken1State(component);

            for (var i = 0; i < processState.ChildProcessCount; i++)
            {
                var processId = Guid.NewGuid();

                _dbContext.Set<ProcessDbEntity<Guid>>().Add(
                    new ProcessDbEntity<Guid>(
                        processId,
                        TestSchemaProcessHandler52.ProcessType.ProcessType,
                        TestSchemaProcessHandler52.ProcessType.ProcessVersion,
                        1,
                        DateTimeOffset.MinValue,
                        false,
                        ProcessStatusEnum.AsyncExecute,
                        null
                        )
                    );

                _dbContext.Set<SchemaProcessDataDbEntity<Guid>>().Add(
                    new SchemaProcessDataDbEntity<Guid>(
                        id: processId,
                        processId: processId,
                        rootTriggerKey: string.Empty,
                        currentTokenId: TestSchemaProcessHandler52.Schema.StartTokenId));

                _dbContext.Set<ParentChildProcessDbEntity>().Add(
                    new ParentChildProcessDbEntity(
                        Guid.NewGuid(),
                        processId: parameters.process.Id,
                        triggerKey: tokenState.TriggerKey,
                        isActive: true, 
                        childProcessId: processId));
            }

            return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                isComplete: true,
                ISchemaProcessHandler.ActivateActionDto.ConditionAction("3", asyncExecuteOrWaitSignal: false));
        }

        private async ValueTask<bool> CheckChildProcessesCompleteAsync(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            var result = await _dbContext.Set<ParentChildProcessDbEntity>()
                .AnyAsync(e => 
                    e.ProcessId == parameters.process.Id 
                    && e.IsActive, cancellationToken);

            return !result;
        }

        #endregion

        #region state

        public static ProcessStateDto CreateProcessState(int childProcessCount)
        {
            return new ProcessStateDto()
            {
                ChildProcessCount = childProcessCount,
            };
        }

        public static ProcessStateDto GetProcessState(ISchemaProcessComponent component)
        {
            var state = (ProcessStateDto)component.ProcessState!;
            return state;
        }

        public static ProcessChildTokenState GetOrCreateToken1State(ISchemaProcessComponent component)
        {
            var state = (ProcessChildTokenState?)component.CurrentTokenState
                ?? new ProcessChildTokenState()
                {
                };
            component.CurrentTokenState = state;

            return state;
        }

        public class ProcessStateDto
            : SchemaProcessStateTypelessHandler.ITypeContainer
        {
            public string? AssemblyQualifiedName { get; set; }

            public required int ChildProcessCount { get; set; }            
        }

        public class ProcessChildTokenState
            : SchemaProcessStateTypelessHandler.ITypeContainer
        {
            public string? AssemblyQualifiedName { get; set; }

            public string? TriggerKey { get; set; }
        }

        #endregion
    }
}
