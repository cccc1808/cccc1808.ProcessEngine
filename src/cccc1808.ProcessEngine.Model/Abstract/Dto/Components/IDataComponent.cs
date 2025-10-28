using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Abstract.Dto.Components
{
    public interface IDataComponent
    {
        object NotTypedData { get; }
    }

    /// <summary>
    /// Контейнер для хранения данных процесса.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IDataComponent<T>
        : IDataComponent
    {
        T Data { get; }
    }    
}
