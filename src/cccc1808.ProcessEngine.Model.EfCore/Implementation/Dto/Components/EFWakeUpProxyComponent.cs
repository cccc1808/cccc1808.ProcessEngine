using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components
{
    public class EFWakeUpProxyComponent<TId>
        : IWakeUpComponent
    {
        private readonly WakeUpProcessDbEntity<TId> _dbEntity;

        public DateTimeOffset SessionStartTimeStamp { get; private init; }

        public DateTimeOffset Timestamp 
        {
            get => _dbEntity.TimeStamp;
            set => _dbEntity.TimeStamp = value;
        }

        public bool IsAsyncExecuting
        {
            get => _dbEntity.IsAsyncExecuting;
            set => _dbEntity.IsAsyncExecuting = value;
        }

        public DateTimeOffset TimerDate
        {
            get => _dbEntity.TimerDate;
            set => _dbEntity.TimerDate = value;
        }

        public EFWakeUpProxyComponent(WakeUpProcessDbEntity<TId> dbEntity)
        {
            _dbEntity = dbEntity;
            SessionStartTimeStamp = dbEntity.TimeStamp;
        }        
    }
}
