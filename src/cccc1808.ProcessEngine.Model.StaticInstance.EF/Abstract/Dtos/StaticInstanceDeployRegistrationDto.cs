using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.StaticInstance.EF.Abstract.Dtos
{
    /// <summary>
    /// Регистрирация версии деплоя статичных процессов.
    /// Версию нужно поднимать при любом изменении перечня процессов.
    /// </summary>
    /// <param name="Version"></param>
    public record StaticInstanceDeployRegistrationDto(
        short Version);
}
