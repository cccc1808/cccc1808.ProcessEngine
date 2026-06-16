using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Handlers;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers
{
    public abstract class BaseSchemaProcessHandler<TId> 
        : ISchemaProcessHandler<TId>
    {
        private readonly Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>> _serviceTaskHandlers;
        private readonly Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>> _checkConditionHandlers;
        private readonly Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>> _executeConditionHandlers;
        private readonly Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>> _timerHandlers;
        
        protected BaseSchemaProcessHandler()
        {
            _serviceTaskHandlers = new Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>>(5);
            _checkConditionHandlers = new Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>>(5);
            _executeConditionHandlers = new Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>>(5);
            _timerHandlers = new Dictionary<string, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>>>(5);
        }
        
        protected void RegistryServiceTask(string key, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>> handler)
        {
            _serviceTaskHandlers.Add(key, handler);
        }

        protected void RegistryCheck(string key, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>> handler)
        {
            _checkConditionHandlers.Add(key, handler);
        }

        protected void RegistryTimer(string key, Func<string, string, IProcessContainer<TId>, CancellationToken, ValueTask<bool>> handler)
        {
            _timerHandlers.Add(key, handler);
        }

        public bool CanExecuteServiceTask(string name)
        {
            return _serviceTaskHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> ExecuteServiceTask(
            string name,
            string actionId, 
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            return await _serviceTaskHandlers[name](name, actionId, process, cancellationToken);
        }

        public bool CanCheckCondition(string name)
        {
            return _checkConditionHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> CheckConditionAsync(
            string name,
            string actionId,
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            return await _checkConditionHandlers[name](name, actionId, process, cancellationToken);
        }

        public bool CanExecuteConditionHandler(string name)
        {
            return _executeConditionHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> ExecuteConditionHandlerAsync(
            string name,
            string actionId, 
            IProcessContainer<TId> process, 
            CancellationToken cancellationToken)
        {
            return await _executeConditionHandlers[name](name, actionId, process, cancellationToken);
        }

        public bool CanExecuteTimer(string name)
        {
            return _timerHandlers.ContainsKey(name);
        }

        public async ValueTask<bool> ExecuteTimerAsync(
            string name,
            string actionId, 
            IProcessContainer<TId> process,
            CancellationToken cancellationToken)
        {
            return await _timerHandlers[name](name, actionId, process, cancellationToken);
        }        
    }
}
