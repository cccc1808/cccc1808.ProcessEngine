using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Implementation.Services.Triggers;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.OutboxModule.Entitites;
using cccc1808.ProcessEngine.Model.IQueryable.Abstract.ProcessModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.OutboxModule.Triggers
{
    /// <summary>
    /// Резервный хендлер, проталкивающий застрявшие процессы (по которым не сработал их персональный триггер).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    internal class OutboxEmergencyTriggerHandler<TId>
        : BaseEmergencyTriggerHandler<TId>
    {
        private readonly Options _options;

        public OutboxEmergencyTriggerHandler(
            IServiceProvider serviceProvider,
            Options options)
            : base(serviceProvider, options)
        {
            _options = options;
        }

        protected override IQueryable<ProcessDbEntity<TId>> Build(
            IServiceProvider serviceProvider,
            IEFDbContext dbContext,
            DateTimeOffset timeout,
            IQueryable<ProcessDbEntity<TId>> source)
        {
            return base.Build(serviceProvider, dbContext, timeout, source)
                .Where(e =>
                    dbContext.Set<OutboxMessageDbEntity<TId>>()
                        .Where(e2 =>
                            e2.ProcessId.Equals(e.Id)
                            && e2.IsActive)
                        .Any() // Есть необработанные сообщения.                               
                        );
        }

        public class Options : BaseEmergencyTriggerHandler<TId>.Options
        {
        }
    }
}
