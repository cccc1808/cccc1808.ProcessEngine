using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;

namespace cccc1808.ProcessEngine.Model.Abstract.ProcessExecutionModule.Services
{
    public interface IProcessRegistry
    {
        ICollection<ProcessRegistryDto> All();

        /// <summary>
        /// TODO: воззможно вернуть IsSignleExecuteProcess. 
        /// Чтобы нода (триггер), даже со старой версией (которая не имеет обрабатывать эту версию процесса), могла ее обработать. 
        /// Иначе нода, где не зарегистрирована версия процесса не сможет выполнить триггер.
        /// </summary>
        /// <param name="processTypeUnique"></param>
        /// <returns></returns>
        ProcessRegistryDto Get(
            ProcessTypeUniqueDto processTypeUnique);
    }
}
