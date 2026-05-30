using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.TriggersModule.Entities;

namespace cccc1808.ProcessEngine.Test.Common
{
    public interface ITestService
    {
        Task RunProcessRunnerAsync(IServiceProvider serviceProvider);

        Task RunTriggerConsumerRunnerAsync(IServiceProvider serviceProvider);

        Task RunTriggerDbRunnerAsync(IServiceProvider serviceProvider);

        Task SendTriggerEventAsync(IServiceProvider serviceProvider, ITriggerEvent[] events, Guid processId);

        Task SendTriggerEventAsync(IServiceProvider serviceProvider, ITriggerEventRaiser<Guid>.RaiseContainer[] events);

        Task<T[]> LoadAsync<T>(IServiceProvider serviceProvider) where T : class;

        Task<ProcessDbEntity<Guid>[]> LoadProcessAsync(IServiceProvider serviceProvider);

        Task<TriggerDbEntity<Guid>[]> LoadTriggersAsync(IServiceProvider serviceProvider);
    }
}
