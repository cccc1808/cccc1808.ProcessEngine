//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using cccc1808.ProcessEngine.Model.Abstract.Repository;
//using cccc1808.ProcessEngine.Model.Implementation.Entitites;

//namespace cccc1808.ProcessEngine.Model.Implementation
//{
//    internal class Helper<TId>
//    {
//        private readonly IProcessRepository<TId> _processRepository;

//        public Task ExecuteWithErrorAsync(
//            ProcessEntity<TId> process, 
//            Func<Task> action,
//            CancellationToken cancellationToken)
//        {
//            try 
//            {
//                await action();
//            }
//            catch(Exception ex)
//            {
//                // SaveError
//                await _processRepository.UpdateAsync(process, cancellationToken);
//            }
//        }
//    }
//}
