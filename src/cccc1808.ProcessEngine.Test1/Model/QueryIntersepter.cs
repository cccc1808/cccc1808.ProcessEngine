//using System;
//using System.Collections.Generic;
//using System.Data.Common;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using Microsoft.EntityFrameworkCore.Diagnostics;

//namespace cccc1808.ProcessEngine.Test1.Model
//{
//    internal class QueryIntersepter : DbCommandInterceptor
//    {
//        public static AsyncLocal<ContainerDto> State { get; }
//            = new AsyncLocal<ContainerDto>();

//        static QueryIntersepter() 
//        {
//            State.Value = new ContainerDto();
//        }

//        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
//            DbCommand command,
//            CommandEventData eventData,
//            InterceptionResult<DbDataReader> result,
//            CancellationToken cancellationToken = default
//            )
//        {
//            if (State.Value.Intersept)
//            {
//                State.Value.Sql = command.Parameters;
//                return
//            }

//            ApplyHints(command);
//            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
//        }

//        public class ContainerDto 
//        {
//            public bool Intersept {  get; set; }
//            public string Sql {  get; set; }
//        }
//    }
//}
