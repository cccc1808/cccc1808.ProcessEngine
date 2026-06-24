using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Dto;
using cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Component;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Entity;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;
using cccc1808.ProcessEngine.Test2.Infrastructure.ParentChild.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
                            Name = "Дочерние процессы 1",
                            Description = "2) Запускаем дочерние процессы",
                            ActivatedOnStart = false,
                            CanRunAction = [new ITokenAction.RunActionDeclarationDto("3", "Ожидаем завершения дочерних процессов")],
                        },
                        new ConditionTokenAction("3", "1_CheckChildComplete")
                        {
                            Name = "Дочерние процессы 2",
                            Description = "Проверят, что все дочерние процессы завершены",
                            ActivatedOnStart = false,
                            Transition = ITokenAction.TransitionDto.Complete(),
                        }
                        )
                    {
                        Name = "Дочерние процессы",
                        Description = "Обработка дочерних процессов",
                    },
                ]
                )
            {
                Description = "Родительский процесс с CounterTrigger"
            };

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(51, 1);

        private readonly IServiceProvider _serviceProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IEFDbContext _dbContext;
        private readonly ITriggerRepository<Guid> _triggerRepository;

        public TestSchemaProcessHandler51(
            IServiceProvider serviceProvider,
            IDateTimeProvider dateTimeProvider,
            IEFDbContext dbContext, 
            ITriggerRepository<Guid> triggerRepository) :
            base()
        {
            _serviceProvider = serviceProvider;
            _dateTimeProvider = dateTimeProvider;
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

        private async ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult> RunChildProcess(
            ISchemaProcessHandler<Guid>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            const int batchSize = 10;

            static async Task CreateBatchAsync(
                IServiceProvider serviceProvider,
                Guid processId,
                ProcessStateDto processState,
                ProcessChildTokenState tokenState,
                LinkContainer<int> lastCreatedIndex,
                CancellationToken cancellationToken)
            {
                var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var dbContext = serviceProvider.GetRequiredService<DbContext>();

                var batchLimit = Math.Min(processState.ChildProcessCount, lastCreatedIndex.Data + batchSize);

                await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                {
                    for (var i = lastCreatedIndex.Data; i < batchLimit; i++)
                    {
                        var createProcessId = Guid.NewGuid();

                        dbContext.Set<ProcessDbEntity<Guid>>().Add(
                            new ProcessDbEntity<Guid>(
                                createProcessId,
                                TestSchemaProcessHandler52.ProcessType.ProcessType,
                                TestSchemaProcessHandler52.ProcessType.ProcessVersion,
                                1,
                                DateTimeOffset.MinValue,
                                false,
                                ProcessStatusEnum.AsyncExecute,
                                null
                                )
                            );

                        dbContext.Set<SchemaProcessDataDbEntity<Guid>>().Add(
                            new SchemaProcessDataDbEntity<Guid>(
                                id: createProcessId,
                                processId: createProcessId,
                                rootTriggerKey: string.Empty,
                                currentTokenId: TestSchemaProcessHandler52.Schema.StartTokenId));

                        dbContext.Set<ParentChildProcessDbEntity>().Add(
                            new ParentChildProcessDbEntity(
                                Guid.NewGuid(),
                                processId: processId,
                                triggerKey: tokenState.TriggerKey,
                                isActive: true,
                                childProcessId: createProcessId,
                                childProcessIndex: i));
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                lastCreatedIndex.Data = batchLimit;
            }            

            var component = parameters.process.GetComponent<ISchemaProcessComponent>();
            var softTimeoutComponent = parameters.process.GetComponent<ISoftTimeoutComponent>();
            var processState = GetProcessState(component);
            var tokenState = GetOrCreateToken1State(component);

            // Получаем метаданные по последнему созданному процессу.
            var lastCreatedChild = await _dbContext.Set<ParentChildProcessDbEntity>()
                .AsNoTracking()
                .OrderByDescending(e => e.ChildProcessIndex)
                .Select(e => new { e.ProcessId, e.ChildProcessIndex })
                .FirstOrDefaultAsync(e => e.ProcessId == parameters.process.Id);

            var lastCreatedChildIndex = LinkContainer.Create(
                lastCreatedChild?.ChildProcessIndex ?? 0);

            var result = await SoftTimeoutHelper.ExecuteWithSoftTimeoutAsync(
                (_serviceProvider, parameters.process, processState, tokenState, lastCreatedChildIndex),
                _dateTimeProvider,
                softTimeoutComponent.StopDate,
                checkComplete: static (p) =>
                {
                    // Все дочерние процессы запущены.
                    return p.lastCreatedChildIndex.Data == p.processState.ChildProcessCount;
                },
                handler: static async (p, t) => 
                {
                    await using (var scope = p._serviceProvider.CreateAsyncScope())
                    {
                        await CreateBatchAsync(
                            scope.ServiceProvider,
                            p.process.Id,
                            p.processState,
                            p.tokenState,
                            p.lastCreatedChildIndex,
                            t);
                    }
                },
                cancellationToken
                );

            if (!result)
            {
                // Продолжаем выполнение в следующей транзакции.
                return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                    isComplete: false);
            }
            else 
            {
                // Переходим в ожидание завершения дочерних процессов.
                return ISchemaProcessHandler.ExecuteServiceTaskResult.Result(
                    isComplete: true,
                    ISchemaProcessHandler.ActivateActionDto.ConditionAction("3", asyncExecuteOrWaitSignal: false));
            }
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
