using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos;

namespace cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Handlers
{
    /// <summary>
    /// Пользовательский хендлер.
    /// Создает и удалеяет пользовательские типы процессов.
    /// [Info] Возможно сделать единичным и указыватьь тип в каждой регистрации.
    /// [Info] Возможно добавить версию и добавить метод обновления процесса при более низкой версии.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public interface IStaticInstanceHandler<TId>
    {
        bool CanProcess(StaticInstanceProcessRegistrationDto staticInstanceRegistration);

        /// <summary>
        /// Сздать экземпляры статичных процессов.
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IDictionary<StaticInstanceProcessRegistrationDto, TId>> CreateProcessRangeAsync(
            ICollection<StaticInstanceProcessRegistrationDto> keys,
            CancellationToken cancellationToken);

        /// <summary>
        /// Удалить экземпляры статичных процессов.
        /// </summary>
        /// <param name="keys"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RemoveProcessRangeAsync(
            ICollection<KeyValuePair<StaticInstanceProcessRegistrationDto, TId>> keys,
            CancellationToken cancellationToken);
    }
}
