using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.Common.Condition;
using cccc1808.ProcessEngine.Model.Abstract.Common.Entities.Conditions;
using cccc1808.ProcessEngine.Model.Abstract.Common.QueryHint;
using cccc1808.ProcessEngine.Model.Abstract.Dto;
using cccc1808.ProcessEngine.Model.Abstract.Dto.Components;
using cccc1808.ProcessEngine.Model.Abstract.Services;
using cccc1808.ProcessEngine.Model.Abstract.Storage.Repository;
using cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Abstract;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Dto.Components;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities;
using cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation.Entities.Conditions;

using Microsoft.EntityFrameworkCore;

namespace cccc1808.ProcessEngine.Model.MessageStream.EntityFramewrokCore.Implementation
{
    public class MessageStreamService<TId, TDbContext> 
        : IMessageStreamTechService<TId> 
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;
        private readonly ILockQueryHintStore _lockQueryHintStore;
        private readonly IProcessSetter _processSetter;
        private readonly IProcessRepository<TId> _processRepository;
        private readonly IId_RangeCondition<TId, TimerProcessDbEntity<TId>> _streamProcess_id_RangeCondition;
        private readonly IId_RangeCondition<TId, StreamActiveDbEntity<TId>> _streamActiveDbEntity_id_RangeCondition;
        private readonly StreamActiveDbEntity_StreamActiveFlag_Condition<TId> _streamActiveDbEntity_StreamActiveFlag_Condition;
        private readonly StreamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition<TId> _streamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition;
        private readonly MessageDbEntity_StreamId_RangeCondition<TId> _messageDbEntity_StreamId_RangeCondition;
        private readonly MessageDbEntity_IsActive_Condition<TId> _messageDbEntity_IsActive_Condition;

        public MessageStreamService(
            TDbContext dbContext,
            ILockQueryHintStore lockQueryHintStore,
            IProcessSetter processSetter,
            IProcessRepository<TId> processRepository)
        {
            _dbContext = dbContext;
            _lockQueryHintStore = lockQueryHintStore;
            _processSetter = processSetter;
            _processRepository = processRepository;

            _streamProcess_id_RangeCondition = new IId_RangeCondition<TId, TimerProcessDbEntity<TId>>();
            _streamActiveDbEntity_id_RangeCondition = new IId_RangeCondition<TId, StreamActiveDbEntity<TId>>();
            _streamActiveDbEntity_StreamActiveFlag_Condition = new StreamActiveDbEntity_StreamActiveFlag_Condition<TId>();
            _streamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition = new StreamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition<TId>();
            _messageDbEntity_StreamId_RangeCondition = new MessageDbEntity_StreamId_RangeCondition<TId>();
            _messageDbEntity_IsActive_Condition = new MessageDbEntity_IsActive_Condition<TId>();            
        }

        public async Task BeforeStreamExecuteAsync(
            ICollection<IProcessContainer<TId>> streams,
            CancellationToken cancellationToken)
        {
            // 1) Фиксируем сведения о исходной дате запуска (без блокировок).
            {
                var activeData = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayFilterCondition(
                        _streamActiveDbEntity_id_RangeCondition,
                        streams.Select(e => e.Id).ToArray())
                    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

                foreach (var elem in streams)
                {
                    elem.AddComponent(
                        new ExecuteContextItemDto()
                        {
                            Component = elem.GetComponent<MessageStreamComponent<TId>>(),
                            StartDate = new SheduleDateDto(activeData[elem.Process.Info.Id.Id].SheduleMinDate),
                        });
                }
            }
        }

        public async Task AfterStreamExecuteAsync(
            ICollection<IProcessContainer<TId>> streams,
            CancellationToken cancellationToken)
        {
            var context = streams
                .Where(e => !e.CurrentSession.HaveError) // TODO: Condition
                .ToDictionary(
                e => e.Process.Info.Id.Id,
                e => (Stream: e, Context: e.GetComponent<ExecuteContextItemDto>()));

            {
                // Сохраняем, чтобы точно применились обновления по статусам сообщений.
                await _processRepository.UpdateAsync(streams, cancellationToken);
            }

            {
                var streamIds = context.Values
                    .Select(e => e.Stream.Id)
                    .ToArray();

                // Блокировка используется, чтобы не допустить ситуации, когда сообщение еще в процессе публикации,
                // а стрим его не увидит и заснет, хотя сообщения есть.
                // Блокировка дожидается завершения всех активных публикаций.
                Dictionary<TId, StreamActiveDbEntity<TId>> actives;
                using (var hint = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    actives = await _dbContext
                        .Set<StreamActiveDbEntity<TId>>()
                        .ApplayFilterCondition(_streamActiveDbEntity_id_RangeCondition, streamIds)
                        .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);
                }

                var activeMessageExsists = await _dbContext.Set<MessageDbEntity<TId>>()
                    .ApplayFilterCondition(_messageDbEntity_StreamId_RangeCondition, streamIds)
                    .ApplayFilterCondition(_messageDbEntity_IsActive_Condition, default)
                    .GroupBy(e => e.StreamId)
                    .ToDictionaryAsync(e => e.Key, e => e.Any(), cancellationToken);

                foreach (var elem in context.Values)
                {
                    elem.Context.LockedActive = actives[elem.Stream.Process.Info.Id.Id];
                    elem.Context.ActiveMessageExists = activeMessageExsists[elem.Stream.Process.Info.Id.Id];
                }
            }

            foreach (var elem in context.Values)
            {
                var currentActiveDate = new SheduleDateDto(
                    elem.Context.LockedActive.SheduleMinDate
                    );
                SheduleDateDto nextExecuteDate;

                // Если дата не менялась с начала обработки (на нее не повлияли новые сообщения)
                if (currentActiveDate.DateUnixMiliseconds == elem.Context.StartDate.DateUnixMiliseconds)
                {
                    // Берем значение из хендлера
                    nextExecuteDate = new SheduleDateDto(
                        elem.Context.HandlerResultDate?.Date ?? DateTimeOffset.MinValue
                        );
                }
                else
                {
                    // Иначе берем минимум из active и хендлера (хендлер или новое сообщение).
                    // Используется, для срочных сообщений (которые нужно обработать в обход стандартной задержки).
                    nextExecuteDate = new SheduleDateDto(
                        Math.Min(
                            currentActiveDate.DateUnixMiliseconds,
                            elem.Context.HandlerResultDate?.DateUnixMiliseconds ?? long.MaxValue
                            )
                        );
                }

                // Задержка следующего срабатывания (если не уснет).
                _processSetter.SetTimer(elem.Stream, nextExecuteDate.Date);
                elem.Context.LockedActive.SheduleMinDate = nextExecuteDate.DateUnixMiliseconds;

                if (elem.Context.ActiveMessageExists)
                {
                    // Стрим остается активным.
                    _processSetter.SetStatus(elem.Stream, ProcessStatusEnum.AsyncExecute);
                }
                else
                {
                    // Стрим засыпает.
                    // Если будет, публикация нового сообщения пробудт стрим.
                    _processSetter.SetStatus(elem.Stream, ProcessStatusEnum.WaitEvent);
                }
            }

            foreach (var elem in streams)
            {
                elem.RemoveComponent<ExecuteContextItemDto>();
            }
        }

        public async Task WakeUpStreamAfterMessageInsertedIfNeedAsync(
            (TId StreamId, DateTimeOffset? delayMinDate)[] data,
            CancellationToken cancellationToken)
        {
            var grouppedData = data
                .GroupBy(e => e.delayMinDate.HasValue)
                .ToArray();

            // Не обновляем меняем дату
            await WakeUpWithoutDateAsync(
                grouppedData.First(e => !e.Key).Select(e => e.StreamId).ToArray(),
                cancellationToken);

            // Обновляем дату, если передана меньше текущей.
            await WakeUpWithDateAsync(
                grouppedData.First(e => e.Key).Select(e => (e.StreamId, e.delayMinDate.Value)).ToArray(),
                cancellationToken);

        }

        private async Task WakeUpWithDateAsync(
            (TId Id, DateTimeOffset delayMinDate)[] data,
            CancellationToken cancellationToken) 
        {
            if (data.Length == 0)
            {
                return;
            }

            var buffer = data.ToDictionary(
                e => e.Id, 
                e => new SheduleDateDto(e.delayMinDate));
            // 1) Если StreamActiveFlag, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
            {
                var actives = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayFilterCondition(
                        _streamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition,
                        (
                            _dbContext,
                            buffer.Select(e => (e.Key, e.Value)).ToArray()
                        ))
                    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

                // Флаг взведен и дата меньше.
                foreach (var elem in buffer)
                {
                    if (actives.ContainsKey(elem.Key))
                    {
                        // Условие выполняется. Действие не требуется.
                        buffer.Remove(elem.Key);
                    }
                    else
                    {
                        // Условие не выполняется.
                    }
                }
            }

            // 2) Ждем UpdateLock.
            {
                StreamActiveDbEntity<TId>[] actives;
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    actives = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                        .AsNoTracking()
                        .ApplayFilterCondition(_streamActiveDbEntity_id_RangeCondition, buffer.Keys)
                        .ToArrayAsync(cancellationToken);                    
                }

                foreach (var elem in actives)
                {
                    if (_streamActiveDbEntity_StreamActiveFlag_TimerDate_RangeCondition.Check(elem, buffer[elem.Id]))
                    {
                        // Кто-то уже обновил, тогда нам не нужно.
                        buffer.Remove(elem.Id);
                    }
                    else
                    {
                        // Нужно обновлять.
                    }
                }
            }

            // 3) Обновляем active и stream
            {
                var actives = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayFilterCondition(_streamActiveDbEntity_id_RangeCondition, buffer.Keys)
                    .ToArrayAsync(cancellationToken);

                var streamIsActiveGroups = actives
                    .GroupBy(e => e.StreamActiveFlag)
                    .ToArray();

                foreach (var elem in actives) 
                {
                    elem.StreamActiveFlag = true;
                    elem.SheduleMinDate = buffer[elem.Id].DateUnixMiliseconds;
                }
 
                {
                    // Стрим не активен, значит он гарантировано не заблокирован, активируем и указываем дату.
                    TimerProcessDbEntity<TId>[] streams;
                    using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                    {
                        streams = await _dbContext.Set<TimerProcessDbEntity<TId>>()
                            .AsNoTracking()
                            .ApplayFilterCondition(
                                _streamProcess_id_RangeCondition, 
                                streamIsActiveGroups.First(e => !e.Key).Select(e => e.Id).ToArray())
                            .ToArrayAsync(cancellationToken);
                    }

                    foreach (var elem in streams)
                    {
                        if (elem.HaveErrorFlag) // TODO: condition
                        {
                            // Если стрим упал в ошибку, то не трогаем его.
                            continue;
                        }

                        elem.Status = ProcessStatusEnum.AsyncExecute;
                        elem.TimerDate = buffer[elem.Id].Date;
                    }
                }

                {
                    // Стрим активен (может исполняться), поэтому обновляем только если не заблокирован.
                    TimerProcessDbEntity<TId>[] streams;
                    using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdateAndSkipLocked))
                    {
                        streams = await _dbContext.Set<TimerProcessDbEntity<TId>>()
                            .AsNoTracking()
                            .ApplayFilterCondition(
                                _streamProcess_id_RangeCondition,
                                streamIsActiveGroups.First(e => !e.Key).Select(e => e.Id).ToArray())
                            .ToArrayAsync(cancellationToken);
                    }

                    foreach (var elem in streams)
                    {
                        if (elem.HaveErrorFlag) // TODO: condition
                        {
                            // Если стрим упал в ошибку, то не трогаем его.
                            continue;
                        }

                        elem.Status = ProcessStatusEnum.AsyncExecute;
                        elem.TimerDate = buffer[elem.Id].Date;
                    }
                }
            }            
        }

        private async Task WakeUpWithoutDateAsync(
            TId[] data,
            CancellationToken cancellationToken) 
        {
            if (data.Length == 0)
            {
                return;
            }

            var buffer = new HashSet<TId>(data);
            // 1) Если StreamActiveFlag, то обновлять ничего не нужно, достаточно ShareLock до конца транзакции.
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForShare))
            {
                var actives = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayFilterCondition(
                        _streamActiveDbEntity_id_RangeCondition,
                        data
                        )
                    .ApplayFilterCondition(_streamActiveDbEntity_StreamActiveFlag_Condition, default)
                    .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

                foreach (var elem in data)
                {
                    if (actives.ContainsKey(elem))
                    {
                        // Условие выполняется. Действие не требуется. Блокировка взята.
                        buffer.Remove(elem);
                    }
                    else
                    {
                        // Условие не выполняется. Блокировка не взята.
                    }
                }
            }

            // 2) Иначе нужнен UpdateLock и необходимо обновление.
            using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
            {
                var actives = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                    .AsNoTracking()
                    .ApplayFilterCondition(
                        _streamActiveDbEntity_id_RangeCondition,
                        buffer
                        )
                    .ToArrayAsync(cancellationToken);

                // У нас монопольная блокировка через updlock.
                foreach (var elem in actives)
                {
                    if (_streamActiveDbEntity_StreamActiveFlag_Condition.Check(elem, default))
                    {
                        // Кто-то уже обновил, тогда нам не нужно.
                        buffer.Remove(elem.Id);
                    }
                    else
                    {
                        // Нужно обновлять.
                    }
                }
            }

            // Обновляем active и stream
            {
                var actives = await _dbContext.Set<StreamActiveDbEntity<TId>>()
                    .ApplayFilterCondition(_streamActiveDbEntity_id_RangeCondition, buffer)
                    .ToArrayAsync(cancellationToken);
                foreach (var elem in actives)
                {
                    elem.StreamActiveFlag = true;
                }

                TimerProcessDbEntity<TId>[] processes;
                using (var _ = _lockQueryHintStore.StartScope(LockHintEnum.ForNoKeyUpdate))
                {
                    processes = await _dbContext.Set<TimerProcessDbEntity<TId>>()
                        .ApplayFilterCondition(_streamProcess_id_RangeCondition, buffer)
                        .ToArrayAsync(cancellationToken);
                }

                foreach (var elem in processes)
                {
                    if (elem.HaveErrorFlag) // TODO: condition
                    {
                        // Если стрим упал в ошибку, то не трогаем его.
                        continue;
                    }

                    elem.Status = ProcessStatusEnum.AsyncExecute;
                    // elem.TimerDate = DateTimeOffset.MinValue.UtcDateTime;
                }
            }
        }

        private class ExecuteContextItemDto
        {
            public MessageStreamComponent<TId> Component { get; init; }

            public SheduleDateDto StartDate { get; init; }
            public SheduleDateDto? HandlerResultDate { get; set; }

            public StreamActiveDbEntity<TId> LockedActive { get; set; }

            public bool ActiveMessageExists { get; set; }
        }
    }
}
