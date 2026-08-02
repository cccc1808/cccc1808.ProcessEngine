using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Test2.Infrastructure
{
    internal static class NameConst
    {
        /// <summary>
        /// Используется для разделения частей в имени ключей (split).
        /// !! Не использовать символ внутри имени.
        /// </summary>
        public static char NamePartsSplitChar { get; } 
            = '|';
    }
}
