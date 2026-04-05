//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
//using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
//using cccc1808.ProcessEngine.Model.EfCore.Implementation.TriggersModule.Services;
//using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

//namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Triggers
//{
//    /// <summary>
//    /// Резервный хендлер, проталкивающий застрявшие процессы (по которым не сработал их персональный триггер).
//    /// </summary>
//    /// <typeparam name="TId"></typeparam>
//    internal class InboxEmergencyTriggerHandler<TId>
//        : BaseEmergencyTriggerHandler<TId>
//    {
//        private readonly Options _options;

//        public InboxEmergencyTriggerHandler(
//            IServiceProvider serviceProvider,
//            Options options)
//            : base (serviceProvider, options)
//        {
//            _options = options;
//        }

//        protected override IQueryable<ProcessDbEntity<TId>> Build(
//            IServiceProvider serviceProvider,
//            IEFDbContext dbContext,
//            DateTimeOffset timeout,
//            IQueryable<ProcessDbEntity<TId>> source)
//        {
//            return base.Build(serviceProvider, dbContext, timeout, source)
//                .Where(e =>
//                    dbContext.Set<InboxMessageDbEntity<TId>>()
//                        .Where(e2 =>
//                            e2.ProcessId.Equals(e.Id)
//                            && e2.IsActive) 
//                        .Any() // Есть необработанные сообщения.                               
//                        );
//        }        

//        public class Options : BaseEmergencyTriggerHandler<TId>.Options
//        {
//        }
//    }


//    internal class InboxEmergencyTriggerHandler2<TId> 
//        : BaseEmergencyKeysetTriggerHandler<TId, InboxProcessDataDbEntity<TId>>
//    {
//        public InboxEmergencyTriggerHandler2(
//            IServiceProvider serviceProvider,
//            Options options)
//            : base(serviceProvider, options)
//        {
//        }

//        protected override IQueryable<InboxProcessDataDbEntity<TId>> Build(
//            IServiceProvider serviceProvider, 
//            IEFDbContext dbContext, 
//            DateTimeOffset timeout)
//        {
//            return base
//                .Build(serviceProvider, dbContext, timeout)
//                .Where(e =>
//                    dbContext.Set<InboxMessageDbEntity<TId>>()
//                        .Where(e2 =>
//                            e2.ProcessId.Equals(e.ProcessId)
//                            && e2.IsActive)
//                        .Any() // Есть необработанные сообщения.                               
//                        );
//        }

//        public class Options : BaseEmergencyKeysetTriggerHandler<TId, InboxProcessDataDbEntity<TId>>.Options
//        {
//        }
//    }
//}
