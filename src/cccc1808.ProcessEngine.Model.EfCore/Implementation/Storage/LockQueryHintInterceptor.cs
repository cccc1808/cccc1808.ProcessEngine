using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Common.QueryHint;

using Microsoft.EntityFrameworkCore.Diagnostics;

namespace cccc1808.ProcessEngine.Model.EfCore.Implementation.Storage
{
    /// <summary>
    /// !! Замечание: 
    /// Простое решение которое работает только для определенных видов запросов.
    /// При использовании необходимо проверить, что формируется нужный sql запрос.
    /// </summary>
    public class LockQueryHintInterceptor
        : DbCommandInterceptor
    {
        private readonly ILockQueryHintStore _queryHintStore;


        public LockQueryHintInterceptor(ILockQueryHintStore queryHintStore)
        {
            _queryHintStore = queryHintStore;
        }


        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
            )
        {
            ApplyHints(command);
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result
            )
        {
            ApplyHints(command);
            return base.ReaderExecuting(command, eventData, result);
        }


        private void ApplyHints(
            DbCommand command
            )
        {
            if (!_queryHintStore.TryGetCurrent(out var scope))
            {
                return;
            }

            switch (scope.Value)
            {
                case LockHintEnum.No:
                    {
                        break;
                    }

                case LockHintEnum.ForNoKeyUpdate:
                    {
                        command.CommandText = $"{command.CommandText} FOR NO KEY UPDATE";

                        break;
                    }

                case LockHintEnum.ForNoKeyUpdateAndSkipLocked:
                    {
                        command.CommandText = $"{command.CommandText} FOR NO KEY UPDATE SKIP LOCKED";

                        break;
                    }

                case LockHintEnum.ForShare:
                    {
                        command.CommandText = $"{command.CommandText} FOR SHARE";

                        break;
                    }

                    //                case QueryHintEnum.__Postgres_AdvisoryLockSubquery:
                    //                    {
                    //                        const string takeParamName = "__take_param";

                    //                        var query = @$"select d.*
                    //from ({command.CommandText}) as d
                    //where
                    //	d.__locked
                    //LIMIT @{takeParamName}";

                    //                        command.CommandText = query;
                    //                        command.Parameters.Add(
                    //                            scope.Take!.Value.StructToDbParameter(takeParamName, NpgsqlTypes.NpgsqlDbType.Integer)
                    //                            );

                    //                        break;
                    //                    }
            }
        }
    }
}
