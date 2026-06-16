using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.CommonModule.Storage
{
    /// <summary>
    /// Генератор Id для БД.
    /// Предпологается для случаев, когда id генерируется на бекенде.
    /// Если id из БД, то реализация должна возвращать значение по умолчанию.
    /// TODO: прикинуть возможность использования sequence.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IIdGenerator<TId>
    {
        ValueTask<TId> NextAsync(CancellationToken cancellationToken);

        ValueTask<Queue<TId>> NextRangeAsync(
            int count, 
            CancellationToken cancellationToken);
    }
}
