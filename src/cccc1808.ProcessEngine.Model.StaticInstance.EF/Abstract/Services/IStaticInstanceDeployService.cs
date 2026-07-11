using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Services
{
    public interface IStaticInstanceDeployService
    {
        void Validate();

        /// <summary>
        /// Выполняет попытку задеплоить зарегистрированные процессы.
        /// Сравнивает метаданные регистрации и знаечния из БД.
        /// Если тип зарегистрирован, но его нет в БД, то процесс будет создан.
        /// Если тип не зарегистрирован, но он есть в БД, то процесс будет удален.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>True - удалось обработать все элементы, False - нужена еще попытка, не удалось получить блокировку.</returns>
        Task<bool> TryExecuteAsync(CancellationToken cancellationToken);
    }
}
