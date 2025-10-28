//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
//using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;

//namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Dto.Components
//{
//    internal class EFTimerProxyComponent<TId>
//        : ITimerProcessComponent<TId>
//    {
//        public TimerProcessDbEntity<TId> TimerProcessDbEntity { get; }

//        public DateTimeOffset TimerDate
//        {
//            get => TimerProcessDbEntity.TimerDate;
//            set => TimerProcessDbEntity.TimerDate = value;
//        }
//        public TId? LinkedProcessId
//        {
//            get => TimerProcessDbEntity.LinkedProcessId;
//            set => TimerProcessDbEntity.LinkedProcessId = value;
//        }
//        public bool IsProcessOrTimer
//        {
//            get => TimerProcessDbEntity.IsProcessOrTimer;
//            set => TimerProcessDbEntity.IsProcessOrTimer = value;
//        }
//        public IProcessContainer<TId>? LinkedProcess { get; set; }

//        public EFTimerProxyComponent(TimerProcessDbEntity<TId> timerProcessDbEntity)
//        {
//            TimerProcessDbEntity = timerProcessDbEntity;
//        }
//    }
//}
