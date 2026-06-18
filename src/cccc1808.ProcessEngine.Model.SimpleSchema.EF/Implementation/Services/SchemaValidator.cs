using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Service;

using static cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions.ITokenAction;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Services
{
    public class SchemaValidator<TId> : ISchemaValidator
    {
        private readonly ISchemaService<TId> _schemaService;

        public SchemaValidator(
            ISchemaService<TId> schemaService)
        {
            _schemaService = schemaService;
        }

        public void Validate(
            ProcessTypeDto processType, 
            ProcessSchemaDto schema,
            bool needComplete = true)
        {
            static void ValidateTransition(
                IReadOnlySet<string> tokenIds,
                ITokenAction.TransitionDto transition)
            {
                if (transition.IsComplete && !string.IsNullOrEmpty(transition.TargetTokenId))
                {
                    throw new Exception($"{nameof(TransitionDto)} не может одновременно и завершать процесс и выполнять переход на токен.");
                }

                if (!transition.IsComplete && string.IsNullOrEmpty(transition.TargetTokenId))
                {
                    throw new Exception($"{nameof(TransitionDto)} должен либо завершать процесс либо вывполнять переход на другой токен.");
                }

                if (!transition.IsComplete)
                { 
                    if (!tokenIds.Contains(transition.TargetTokenId))
                    {
                        throw new Exception($"{nameof(TransitionDto)} ссылается на токен, который не объявлен в схеме.");
                    }
                }
            }

            if (!schema.Tokens.Any())
            {
                throw new Exception("Схема не содержит ни одного токена.");
            }            

            var tokenIds = schema.Tokens.Keys.ToHashSet();

            if (!tokenIds.Contains(schema.StartTokenId))
            {
                throw new Exception("Схема не содержит стартового токена.");
            }

            var containsComplete = false;
            var handler = _schemaService.GetProcessHandler(processType);
            var stateHandler = _schemaService.GetProcessStateHandler(processType);

            foreach (var elem in schema.Tokens.Values)
            {
                if (!elem.Actions.Any())
                {
                    throw new Exception("Не зарегистрировано ни одного действия.");
                }

                if (!stateHandler.IsTokenSupport(elem.Id))
                {
                    throw new Exception($"Не реализована сериализация состояния токена {elem.Id}");
                }

                var actionIds = elem.Actions.Select(e => e.Id).ToHashSet();
                if (actionIds.Count != elem.Actions.Length)
                {
                    throw new Exception("Есть действия с повторяющимя именем.");
                }

                var haveTransition = false;
                var haveActiveOnStart = false;
                var activeOnStartHaveTransitionOrActivation = false;
                foreach (var elem2 in elem.Actions)
                {
                    if (elem2.ActivatedOnStart)
                    {
                        haveActiveOnStart = true;
                    }

                    switch (elem2)
                    {
                        case TimerTokenAction timerTokenAction: 
                            {
                                var haveAction = false;
                                if (elem2.ActivatedOnStart)
                                {
                                    if (timerTokenAction.Transition.HasValue || timerTokenAction.CanRunAction.Any())
                                    {
                                        activeOnStartHaveTransitionOrActivation = true;
                                    }
                                }

                                if (timerTokenAction.HandlerKey is not null)
                                {
                                    if (!handler.CanExecuteTimer(timerTokenAction.HandlerKey))
                                    {
                                        throw new Exception(timerTokenAction.HandlerKey);
                                    }

                                    haveAction = true;                                    
                                }

                                if (timerTokenAction.Transition.HasValue)
                                {
                                    ValidateTransition(tokenIds, timerTokenAction.Transition.Value);

                                    haveAction = true;
                                    haveTransition = true;

                                    if (timerTokenAction.Transition.Value.IsComplete)
                                    {
                                        containsComplete = true;
                                    }
                                }

                                if (!haveAction)
                                {
                                    throw new Exception("TimerTokenAction не содержит не хенндлера ни перехода.");
                                }
                                
                                break;
                            }

                        case ConditionTokenAction conditionTokenAction:
                            {
                                var haveAction = false;
                                if (elem2.ActivatedOnStart)
                                {
                                    if (conditionTokenAction.Transition.HasValue || conditionTokenAction.CanRunAction.Any())
                                    {
                                        activeOnStartHaveTransitionOrActivation = true;
                                    }
                                }

                                if (!handler.CanCheckCondition(conditionTokenAction.CheckHandlerKey))
                                {
                                    throw new Exception(conditionTokenAction.CheckHandlerKey);
                                }

                                if (conditionTokenAction.ActionHandlerKey is not null)
                                {
                                    if (!handler.CanExecuteConditionHandler(conditionTokenAction.ActionHandlerKey))
                                    {
                                        throw new Exception(conditionTokenAction.ActionHandlerKey);
                                    }

                                    haveAction = true;
                                }

                                if (conditionTokenAction.Transition.HasValue)
                                {
                                    ValidateTransition(tokenIds, conditionTokenAction.Transition.Value);                                    

                                    haveAction = true;
                                    haveTransition = true;

                                    if (conditionTokenAction.Transition.Value.IsComplete)
                                    {
                                        containsComplete = true;
                                    }
                                }

                                if (!haveAction)
                                {
                                    throw new Exception("TimerTokenAction не содержит не хенндлера ни перехода.");
                                }

                                break;
                            }

                        case ServiceTaskTokenAction serviceTaskTokenAction:
                            {
                                if (elem2.ActivatedOnStart)
                                {
                                    if (serviceTaskTokenAction.Transition.HasValue || serviceTaskTokenAction.CanRunAction.Any())
                                    {
                                        activeOnStartHaveTransitionOrActivation = true;
                                    }
                                }

                                if (!handler.CanExecuteServiceTask(serviceTaskTokenAction.HandlerKey))
                                {
                                    throw new Exception(serviceTaskTokenAction.HandlerKey);
                                }

                                if (serviceTaskTokenAction.Transition.HasValue)
                                {
                                    ValidateTransition(tokenIds, serviceTaskTokenAction.Transition.Value);

                                    if (serviceTaskTokenAction.Transition.Value.IsComplete)
                                    {
                                        containsComplete = true;
                                    }

                                    haveTransition = true;
                                }
                                
                                break;
                            }                    
                    }

                    foreach (var elem3 in elem2.CanRunAction)
                    {
                        if (!actionIds.Contains(elem3.ActivateActionId))
                        {
                            throw new Exception(
                                $"Задекларирован переход, но действие не найдено. {elem.Id}. {elem2.Id}. {elem3.ActivateActionId}.");
                        }
                    }
                }

                if (!haveTransition)
                {
                    throw new Exception("Токен не содержит перехода.");
                }    
                
                if (!haveActiveOnStart)
                {
                    throw new Exception("Токен не содержит ни одного активного на старте действия.");
                }

                if (!activeOnStartHaveTransitionOrActivation)
                {
                    throw new Exception("Ни одно активное на старте действие не содержит переходов и активаций других действий.");
                }
            }

            if (needComplete)
            {
                if (!containsComplete)
                {
                    throw new Exception("Не найдено ни одного перехода на завершения процесса.");
                }
            }

            // TODO: можно по условию валидировать достижимость всех токенов (до каждого токена сущетсвует граф переходов от старта).
        }
    }
}
