using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.CommonModule.Storage;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.MessageStreamModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Conditions;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.ProcessModule.Entities;
using cccc1808.ProcessEngine.Model.Implementation.ConditionModule;
using cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.InboxModule.Entitites;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Implementation.InboxModule.Triggers
{
    /// <summary>
    /// Резервный хендлер, проталкивающий застрявшие процессы (по которым не сработал их персональный триггер).
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    internal class IInboxEmergencyTriggerHandler<TId>
        : ITriggerSingleHandler<TId>
    {
        private readonly IEFDbContext _dbContext;
        private readonly IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> _messageStreamConditions;
        private readonly IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> _processDbEntityConditions;
        private readonly Options _options;

        public IInboxEmergencyTriggerHandler(
            IEFDbContext dbContext,
            IMessageStreamConditions<TId, InboxMessageDbEntity<TId>> messageStreamConditions,
            IProcessDbEntityConditions<TId, ProcessDbEntity<TId>> processDbEntityConditions,
            Options options)
        {
            _dbContext = dbContext;
            _messageStreamConditions = messageStreamConditions;
            _processDbEntityConditions = processDbEntityConditions;
            _options = options;
        }

        public async ValueTask<ITriggerHandler.Result> HandleAsync(
            ITriggerComponent<TId> trigger,
            CancellationToken cancellationToken)
        {
            // todo:
            // soft timeout
            // Отбираем батч спящих процессов, у которых есть непрочитанные сообщения.
            // При этом их тригер давно не выполнялся.
            // Пропускаем заблокированные.
            // Запускаем процессы.

            var timeout = DateTimeOffset.UtcNow.Add(-_options.Timeout);

            var haveNotProcessed = true;
            while (haveNotProcessed) 
            {               
                // transaction

                var stoppedProcessesIds = await _dbContext.Set<InboxProcessDataDbEntity<TId>>()
                    .Where(
                        e =>                            
                            _dbContext.Set<ProcessDbEntity<TId>>()
                                .Where(e2 => e2.Id.Equals(e.ProcessId))
                                .ApplayQueryCondition(
                                    _processDbEntityConditions.MaybeStoppedByTriggerEventLoosed.QueryRange, 
                                    timeout)
                                .Any()
                            // Не обязательно проверять.
                            //&& _dbContext.Set<TriggerDbEntity<TId>>()
                            //    .Where(e2 =>
                            //        e2.Key == e.WakeupTriggerKey
                            //        && e2.HandlerDate < timeout) // 4) Последний раз триггер срабатывал давно.
                            //    .Any()
                            && _dbContext.Set<InboxMessageDbEntity<TId>>()
                                .Where(e2 => e2.ProcessId.Equals(e.ProcessId))
                                .ApplayQueryCondition(_messageStreamConditions.IsActiveMessages.Query)
                                .Any()
                            )
                    .Take(_options.BatchSize)
                    .Select(e => e.ProcessId)
                    .ToArrayAsync(cancellationToken);

                haveNotProcessed = _options.BatchSize == stoppedProcessesIds.Length;

                // soft timeout
                if (false)
                {
                    break;
                }

                if (stoppedProcessesIds.Any())
                {
                    // skip locked.
                    await _dbContext
                        .Set<ProcessDbEntity<TId>>()
                        .Where(
                            e => 
                            stoppedProcessesIds.Contains(e.Id) 
                            && e.Status == ProcessStatusEnum.WaitEvent 
                            && !e.StoppedByError
                            && e.RetryCount == null)
                        .ExecuteUpdateAsync(e => e.SetProperty(e => e.Status, ProcessStatusEnum.AsyncExecute), cancellationToken);
                }
                else 
                {
                    break;
                }
            }            

            if (haveNotProcessed)
            {
                return new ITriggerHandler.Result(true, true, DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5));
            }
            else 
            {
                return new ITriggerHandler.Result(true, true, DateTimeOffset.MinValue);
            }
        }

        public class Options 
        {
            public int BatchSize { get; set; }

            public TimeSpan Timeout { get; set; }
        }
    }
}
