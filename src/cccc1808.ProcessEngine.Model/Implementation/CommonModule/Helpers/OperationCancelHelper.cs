using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers
{
    public static class OperationCancelHelper
    {
        public static bool IsCancelException(Exception ex, CancellationToken cancellationToken)
        {
            return 
                ex is OperationCanceledException 
                && cancellationToken.IsCancellationRequested;
        }
    }
}
