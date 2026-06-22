using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;

namespace cccc1808.ProcessEngine.Model.Implementation.CommonModule.Helpers
{
    public static class SoftTimeoutHelper
    {
        public static async ValueTask<bool> ExecuteWithSoftTimeoutAsync<TParameters>(
            TParameters parameters,
            IDateTimeProvider dateTimeProvider,
            DateTimeOffset? softTimeoutValue,          
            Func<TParameters, bool> checkComplete,
            Func<TParameters, CancellationToken, ValueTask> handler,
            CancellationToken cancellationToken) 
        {
            if (checkComplete(parameters))
            {
                return true;
            }

            while (true) 
            {
                if (dateTimeProvider.UtcNow >= softTimeoutValue)
                {
                    return false;
                }
                
                await handler(parameters, cancellationToken);

                if (checkComplete(parameters))
                {
                    return true;
                }
            }
        }
    }
}
