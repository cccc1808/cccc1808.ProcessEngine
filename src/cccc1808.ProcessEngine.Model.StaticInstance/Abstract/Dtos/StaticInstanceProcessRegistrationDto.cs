using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.StaticInstance.Abstract.Dtos
{
    /// <summary>
    /// Регистрация статичного процесса.
    /// </summary>
    /// <param name="ProcessType">Тип процесса.</param>
    /// <param name="Key">Ключ уникальности.</param>
    public record StaticInstanceProcessRegistrationDto(
        long ProcessType,
        string Key)
    {
        public override int GetHashCode()
        {
            return HashCode.Combine(
                ProcessType.GetHashCode(), 
                Key.GetHashCode());
        }

        public override string ToString()
        {
            return $"{ProcessType} | {Key}";
        }
    }
}
