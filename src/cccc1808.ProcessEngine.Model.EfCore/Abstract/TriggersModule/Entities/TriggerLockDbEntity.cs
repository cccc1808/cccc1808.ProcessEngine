using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.TriggersModule.Entities
{
    /// <summary>
    /// Отдельная выделенная таблица для хранения select lock.
    /// Особенность: имеет пониженные требования к сохранению.
    /// Рекомендуется использовать в режиме unlogged и inmemory tablespace, 
    /// таким образом понижается нагрузка на жесткий диск.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class TriggerLockDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; }

        public DateTimeOffset LockDate { get; set; }

        [Obsolete("Для EF и запросов.")]
        public TriggerLockDbEntity() 
        {
            Id = default!;
        }

        public TriggerLockDbEntity(
            TId id, 
            DateTimeOffset lockDate)
        {
            Id = id;
            LockDate = lockDate;
        }
    }
}
