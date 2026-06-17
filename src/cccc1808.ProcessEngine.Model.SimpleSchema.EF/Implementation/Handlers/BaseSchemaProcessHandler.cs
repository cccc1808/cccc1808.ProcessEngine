using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers
{
    public abstract class BaseSchemaProcessHandler<TId> 
        : ISchemaProcessHandler<TId>
    {
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>> _serviceTaskHandlers;
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>> _checkConditionHandlers;
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask>> _executeConditionHandlers;
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>> _timerHandlers;
        
        protected BaseSchemaProcessHandler()
        {
            _serviceTaskHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>>(5);
            _checkConditionHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>>(5);
            _executeConditionHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask>>(5);
            _timerHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>>(5);
        }
        
        protected void RegistryServiceTask(string key, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>> handler)
        {
            _serviceTaskHandlers.Add(key, handler);
        }

        protected void RegistryConditionTaskCheck(string key, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>> handler)
        {
            _checkConditionHandlers.Add(key, handler);
        }

        protected void RegistryConditionTaskExecute(string key, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask> handler)
        {
            _executeConditionHandlers.Add(key, handler);
        }

        protected void RegistryTimerTask(string key, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>> handler)
        {
            _timerHandlers.Add(key, handler);
        }

        public bool CanExecuteServiceTask(string name)
        {
            return _serviceTaskHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> ExecuteServiceTask(
            ISchemaProcessHandler<TId>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return await _serviceTaskHandlers[parameters.handlerName](parameters, cancellationToken);
        }

        public bool CanCheckCondition(string name)
        {
            return _checkConditionHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> CheckConditionAsync(
            ISchemaProcessHandler<TId>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return await _checkConditionHandlers[parameters.handlerName](parameters, cancellationToken);
        }

        public bool CanExecuteConditionHandler(string name)
        {
            return _executeConditionHandlers.ContainsKey(name);
        }

        public async ValueTask ExecuteConditionHandlerAsync(
            ISchemaProcessHandler<TId>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            await _executeConditionHandlers[parameters.handlerName](parameters, cancellationToken);
        }

        public bool CanExecuteTimer(string name)
        {
            return _timerHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> ExecuteTimerAsync(
            ISchemaProcessHandler<TId>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return await _timerHandlers[parameters.handlerName](parameters, cancellationToken);
        }
    }
}
