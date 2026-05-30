using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.Abstract.WakeupModule.Services;
using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;

using LinqToDB.Async;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    /// <summary>
    /// <see cref="ParentCheckWakeupHandler"/>.
    /// </summary>
    internal class ParentProcessTriggerHandler
        : ITriggerSingleHandler<Guid>
    {
        public const string Name = "ParentProcessTriggerHandler";

        private readonly ILinq2DbDataConnection _dataConnection;
        private readonly IWakeupService<Guid> _wakeupService;

        public ParentProcessTriggerHandler(
            ILinq2DbDataConnection dataConnection,
            IWakeupService<Guid> wakeupService)
        {
            _dataConnection = dataConnection;
            _wakeupService = wakeupService;
        }

        public async ValueTask<ITriggerHandler.Result> HandleAsync(
            ITriggerComponent<Guid> trigger, 
            CancellationToken cancellationToken)
        {
            var notCompletedChilds = await _dataConnection
                .Set<ChildProcessDbEntity>()
                .CountAsync(
                    e => e.ActiveParentProcessId == trigger.ProcessId,
                    cancellationToken);

            if (notCompletedChilds == 0)
            {
                // Все дочерние процессы завершились.

                await _wakeupService.WakeupProcessHandlerAsync(
                    [trigger.ProcessId],
                    useShareLock: false,
                    cancellationToken);
                return new ITriggerHandler.Result(false, false, DateTimeOffset.MinValue);
            }
            else 
            {
                // Есть незавершенные дочерние процессы.
                return new ITriggerHandler.Result(
                    // Тригер не завершен
                    true,
                    // Выелючен - активируется когда придет хотя бы одно событие от ребенка
                    false,
                    // Задержка, чтобы триггер активировался не по первому сообщению (если незавершенных процессов много)
                    // формулу можно уточнить.
                    DateTimeOffset.Now + TimeSpan.FromSeconds(1) * notCompletedChilds                    
                    );
            }
        }
    }
}
