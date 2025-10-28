//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//using cccc1808.ProcessEngine.Model.Abstract.Dto;
//using cccc1808.ProcessEngine.Model.Abstract.Repository;
//using cccc1808.ProcessEngine.Model.Implementation.Entitites;
//using cccc1808.ProcessEngine.Model.Implementation.ProcessExecuteMiddlewares.Execute;

//namespace cccc1808.ProcessEngine.Model.Implementation
//{
//    internal class RetryJobHandler<TId>
//        : ExecuteJobRangeMiddleware<TId>.IHandler
//    {
//        private readonly IRetryTimerRepository<TId> _retryTimerRepository;
//        private readonly ITimerProcessRepository<TId> _timerProcessRepository;

//        public Task<IReadOnlyDictionary<TId, ProcessEntity<TId>>> LoadWithLockRangeSkipLockedAsync(
//            IReadOnlyList<IReadOnlyList<ProcessInstanceInfoDto<TId>>> ids,
//            CancellationToken cancellationToken)
//        {
//            throw new NotImplementedException();
//        }

//        public async ValueTask HandleAsync(
//            IReadOnlyDictionary<TId, ProcessEntity<TId>> processes,
//            CancellationToken cancellationToken)
//        {
//            var timerIds = processes
//                .Select(e => e.Value.Info.Id)
//                .ToArray();

//            // Обрабатываем процесс.
//            await _retryTimerRepository.RetryMainProcessRangeAsync(timerIds, cancellationToken);

//            // Удаляем таймер.
//            await _timerProcessRepository.DeleteRangeAsync(
//                timerIds,
//                cancellationToken);
//        }  

//        public ValueTask OnExceptionRangeAsync(
//            IReadOnlyDictionary<TId, ProcessEntity<TId>> processes, 
//            Exception ex, 
//            CancellationToken cancellationToken)
//        {
//            throw new NotImplementedException();
//        }
//    }
//}
