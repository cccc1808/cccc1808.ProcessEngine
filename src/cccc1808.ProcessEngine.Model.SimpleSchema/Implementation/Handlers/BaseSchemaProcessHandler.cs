using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Handlers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Implementation.Handlers
{
    public abstract class BaseSchemaProcessHandler<TId> 
        : ISchemaProcessHandler<TId>
    {
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult>>> _serviceTaskHandlers;
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>> _checkConditionHandlers;
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteConditionResult>>> _executeConditionHandlers;
        private readonly Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteTimerResult>>> _timerHandlers;
        
        protected BaseSchemaProcessHandler()
        {
            _serviceTaskHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult>>>(5);
            _checkConditionHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>>>(5);
            _executeConditionHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteConditionResult>>>(5);
            _timerHandlers = new Dictionary<string, Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteTimerResult>>>(5);
        }

        #region Registry

        protected void RegistryServiceTask(
            string key, 
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteServiceTaskResult>> handler)
        {
            _serviceTaskHandlers.Add(key, handler);
        }

        protected void RegistryServiceTask(
            string key, 
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ISchemaProcessHandler.ExecuteServiceTaskResult> handler)
        {
            _serviceTaskHandlers.Add(
                key, 
                (p, t) => ValueTask.FromResult(handler(p, t)));
        }

        protected void RegistryConditionTaskCheck(
            string key, 
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<bool>> handler)
        {
            _checkConditionHandlers.Add(key, handler);
        }

        protected void RegistryConditionTaskCheck(
            string key,
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, bool> handler)
        {
            _checkConditionHandlers.Add(
                key, 
                (p,t) => ValueTask.FromResult(handler(p, t)));
        }

        protected void RegistryConditionTaskExecute(
            string key, 
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteConditionResult>> handler)
        {
            _executeConditionHandlers.Add(key, handler);
        }

        protected void RegistryConditionTaskExecute(
            string key,
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ISchemaProcessHandler.ExecuteConditionResult> handler)
        {
            _executeConditionHandlers.Add(
                key,
                (p, t) => ValueTask.FromResult(handler(p, t)));
        }

        protected void RegistryTimerTask(
            string key, 
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ValueTask<ISchemaProcessHandler.ExecuteTimerResult>> handler)
        {
            _timerHandlers.Add(key, handler);
        }

        protected void RegistryTimerTask(
            string key,
            Func<ISchemaProcessHandler<TId>.ExecuteParametersDto, CancellationToken, ISchemaProcessHandler.ExecuteTimerResult> handler)
        {
            _timerHandlers.Add(
                key,
                (p, t) => ValueTask.FromResult(handler(p,t)));
        }

        #endregion

        #region ISchemaProcessHandler

        public bool CanExecuteServiceTask(string name)
        {
            return _serviceTaskHandlers.ContainsKey(name);
        }

        public async ValueTask<ISchemaProcessHandler<TId>.ExecuteServiceTaskResult> ExecuteServiceTask(
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

        public async ValueTask<ISchemaProcessHandler<TId>.ExecuteConditionResult> ExecuteConditionHandlerAsync(
            ISchemaProcessHandler<TId>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return await _executeConditionHandlers[parameters.handlerName](parameters, cancellationToken);
        }

        public bool CanExecuteTimer(string name)
        {
            return _timerHandlers.ContainsKey(name);
        }

        public async ValueTask<ISchemaProcessHandler<TId>.ExecuteTimerResult> ExecuteTimerAsync(
            ISchemaProcessHandler<TId>.ExecuteParametersDto parameters,
            CancellationToken cancellationToken)
        {
            return await _timerHandlers[parameters.handlerName](parameters, cancellationToken);
        }

        #endregion
    }
}
