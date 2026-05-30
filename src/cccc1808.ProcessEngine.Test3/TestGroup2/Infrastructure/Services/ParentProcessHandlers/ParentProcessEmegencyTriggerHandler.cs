using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Linq2Db.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.Linq2Db.Implementation.WakeUpModule.TriggerHandlers;

namespace cccc1808.ProcessEngine.Test3.TestGroup2.Infrastructure.Services
{
    internal class ParentProcessEmegencyTriggerHandler
        : BaseEmergencyKeysetTriggerHandler<Guid, ParentProcessDataDbEntity>
    {
        public const string Name = "ParentProcessEmegencyTriggerHandler";

        public ParentProcessEmegencyTriggerHandler(
            IServiceProvider serviceProvider, 
            ParentProcessEmegencyTriggerHandler.Options options)
            : base(serviceProvider, options)
        {
        }

        protected override IQueryable<ParentProcessDataDbEntity> Build(
            IServiceProvider serviceProvider, 
            ILinq2DbDataConnection dataConnection, 
            DateTimeOffset timeout)
        {
            return base.Build(serviceProvider, dataConnection, timeout)
                .Where(
                        e =>
                            !dataConnection.Set<ChildProcessDbEntity>()
                                .Where(e2 => e2.ActiveParentProcessId.Equals(e.ProcessId))
                                .Any()                                
                                );
        }

        public new class Options 
            : BaseEmergencyKeysetTriggerHandler<Guid, ParentProcessDataDbEntity>.Options
        {

        }
    }
}
