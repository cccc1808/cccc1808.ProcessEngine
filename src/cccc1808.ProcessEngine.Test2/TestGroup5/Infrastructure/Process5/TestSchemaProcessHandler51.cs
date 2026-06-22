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
                            Name = "Создаем триггер для дочерних процессов",
                            ActivatedOnStart = true,
                        },
                        new ServiceTaskTokenAction("2", "1_RunChildProcesses")
                        {
                            Name = "Запускаем дочерние процессы",
                            ActivatedOnStart = false,
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
                    },
                ]
                )
            {
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
                CancellationToken cancellationToken)
            {
                var dateTimeProvider = serviceProvider.GetRequiredService<IDateTimeProvider>();
                var transactionManager = serviceProvider.GetRequiredService<ITransactionManager>();
                var dbContext = serviceProvider.GetRequiredService<DbContext>();

                var batchLimit = Math.Min(processState.ChildProcessCount, tokenState.CreatedChildrenProcessCount.Value + batchSize);
                var createTimestamp = dateTimeProvider.UtcNow.Ticks;

                await using (var transaction = await transactionManager.StartTransactionAsync(cancellationToken))
                {
                    for (var i = tokenState.CreatedChildrenProcessCount.Value; i < batchLimit; i++)
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
                                timeStamp: createTimestamp,
                                processId: processId,
                                triggerKey: tokenState.TriggerKey,
                                isActive: true,
                                childProcessId: createProcessId));
                    }

                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }

                tokenState.CreatedChildrenProcessCount = batchLimit;
                tokenState.LastCreatedChildTimestamp = createTimestamp;
            }            

            var component = parameters.process.GetComponent<ISchemaProcessComponent>();
            var softTimeoutComponent = parameters.process.GetComponent<ISoftTimeoutComponent>();
            var processState = GetProcessState(component);
            var tokenState = GetOrCreateToken1State(component);

            // Получаем метаданные по последнему созданному процессу.
            var lastCreatedChild = await _dbContext.Set<ParentChildProcessDbEntity>()
                .AsNoTracking()
                .OrderByDescending(e => e.TimeStamp)
                .Select(e => new { e.TimeStamp, e.ProcessId })
                .FirstOrDefaultAsync(e => e.ProcessId == parameters.process.Id);

            if (lastCreatedChild is null)
            {
                // Ни одного дочернего процесса еще не создано.
                tokenState.CreatedChildrenProcessCount = 0;
                tokenState.LastCreatedChildTimestamp = null;
            }
            else if (lastCreatedChild.TimeStamp != tokenState.LastCreatedChildTimestamp)
            {
                // Значение указателя последнего созданного дочернего процесса в состоянии токена не совпадает с фактическим значенеим из БД.
                // (Это говорит о том, что транзакция по созданию была закоммичена, а транзакция процесса упала и не обновила счетчик).
                // Поэтому нам необходимо пересчитать количество созданных дочерних процессов.
                var createdChildersCount = await _dbContext.Set<ParentChildProcessDbEntity>()
                    .Where(e => e.ProcessId == parameters.process.Id)
                    .CountAsync();

                tokenState.CreatedChildrenProcessCount = createdChildersCount;
                tokenState.LastCreatedChildTimestamp = lastCreatedChild.TimeStamp;
            }
            else 
            {
                // Колчиство дочерних процессов в состоянии токена корректное.
            }

            if (tokenState.CreatedChildrenProcessCount is null)
            {
                throw new Exception("[Bug] Некорректное состояние процесса. Ожидается наличие значения.");
            }

            var result = await SoftTimeoutHelper.ExecuteWithSoftTimeoutAsync(
                (_serviceProvider, parameters.process, processState, tokenState),
                _dateTimeProvider,
                softTimeoutComponent.StopDate,
                checkComplete: static (p) =>
                {
                    // Все дочерние процессы запущены.
                    return p.tokenState.CreatedChildrenProcessCount == p.processState.ChildProcessCount;
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

            public int? CreatedChildrenProcessCount { get; set; }

            public long? LastCreatedChildTimestamp { get; set; }
        }

        #endregion
    }
}
