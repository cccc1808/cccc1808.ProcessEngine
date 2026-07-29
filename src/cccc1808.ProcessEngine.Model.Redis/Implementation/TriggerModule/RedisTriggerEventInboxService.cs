using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.CommonModule;
using cccc1808.ProcessEngine.Model.Abstract.QueueModule.Dto;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Events;
using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services;
using cccc1808.ProcessEngine.Model.Redis.Abstract.Common.Storage;

using StackExchange.Redis;

namespace cccc1808.ProcessEngine.Model.Redis.Implementation.TriggerModule
{
    public class RedisTriggerEventInboxService
        : ITriggerEventInboxService
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IRedisConnectionFactory _redisConnectionFactory;

        private readonly OptionsDto _options;

        public RedisTriggerEventInboxService(
            IDateTimeProvider dateTimeProvider, 
            IRedisConnectionFactory redisConnectionFactory, 
            
            OptionsDto options)
        {
            _dateTimeProvider = dateTimeProvider;
            _redisConnectionFactory = redisConnectionFactory;

            _options = options;
        }

        public async ValueTask<ITriggerEventInboxService.IContext> FilterMessagesAsync(
            Dictionary<string, List<(MessageDto Message, ITriggerEvent Event)>> groupByTriggerMessages, 
            Dictionary<ITriggerEventInboxService.PartitionKey, ITriggerEventInboxService.PartitionOffset> offsetsData,
            int allMessageCount,
            CancellationToken cancellationToken)
        {
            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DatabaseId);

            var now = _dateTimeProvider.UtcNow;
            var expireTimeout = (now + _options.ExpireTimeout).UtcDateTime;
            var key = _options.HashKeyFactory();
            var sortedKey = _options.SortedSetKeyFactory();

            var resultContext = new Context() 
            {
                NeedCommit = new List<ContextEntryDto>(allMessageCount),
            };
            var localContext = new List<ContextEntryDto>(allMessageCount);            
            {
                var uniques = new HashSet<string>(allMessageCount);
                var piplineTasks = new List<Task>(allMessageCount * 5);

                foreach (var elem in groupByTriggerMessages)
                {
                    for (var i = 0; i < elem.Value.Count; i++)
                    {
                        var elem2 = elem.Value[i];

                        var contextEntry = new ContextEntryDto()
                        {
                            MessageKey = GetMessageKey(elem2.Message),

                            IsProcessedKey = null,
                            TimeStampKey = null,

                            InsertTask = null,
                            GetIsProcessedTask = null,
                            GetTimeStampTask = null,
                        };

                        if (uniques.Contains(contextEntry.MessageKey))
                        {
                            // Дублирование по ключу.
                            elem.Value.RemoveAt(i);
                            i--;
                            continue;
                        }
                        uniques.Add(contextEntry.MessageKey);

                        {
                            contextEntry = contextEntry with
                            {
                                TimeStampKey = _options.StartProcessTimeStampKeyFactory(contextEntry.MessageKey),
                                IsProcessedKey = _options.IsProcessedKeyFactory(contextEntry.MessageKey),
                            };

                            {
                                var t1 = db.HashSetAsync(key, contextEntry.IsProcessedKey, 0, when: When.NotExists);
                                var t2 = db.HashSetAsync(key, contextEntry.TimeStampKey, now.UtcTicks, when: When.NotExists);
                                var t3 = db.HashGetAsync(key, contextEntry.IsProcessedKey);
                                var t4 = db.HashGetAsync(key, contextEntry.TimeStampKey);

                                contextEntry = contextEntry with
                                {
                                    InsertTask = t1,
                                    GetIsProcessedTask = t3,
                                    GetTimeStampTask = t4
                                };

                                piplineTasks.Add(t1);
                                piplineTasks.Add(t2);
                                piplineTasks.Add(t3);
                                piplineTasks.Add(t4);
                            }

                            switch (_options.ClearPolicy)
                            {
                                case OptionsDto.ClearPolicyEnum.ExpireDate:
                                    {
                                        var t5 = db.HashFieldExpireAsync(key, [contextEntry.IsProcessedKey, contextEntry.TimeStampKey], expireTimeout);
                                        piplineTasks.Add(t5);

                                        break;
                                    }

                                case OptionsDto.ClearPolicyEnum.SizeLimit:
                                    {
                                        var t5 = db.SortedSetAddAsync(sortedKey, contextEntry.MessageKey, expireTimeout.Ticks, when: SortedSetWhen.NotExists);

                                        break;
                                    }

                                default: 
                                    throw new NotImplementedException(
                                        _options.ClearPolicy.ToString());
                            }

                            localContext.Add(contextEntry);
                        }
                    }
                }

                await connection.WaitPiplineWithTimeoutAsync(
                    piplineTasks,
                    cancellationToken);
            }

            {
                var forRemove = new HashSet<string>(0);
                foreach (var elem in localContext)
                {
                    var state = elem.BuildState().Value;

                    if (state.IsInserted)
                    {
                        // Новое сообщение.
                        resultContext.NeedCommit.Add(elem);
                        continue;
                    }

                    if (state.IsProcessed)
                    {
                        // Сообщение уже обратона.
                        forRemove.Add(elem.MessageKey);
                        continue;
                    }

                    {
                        // Сообщение не подтверждено.
                        var delta = now - state.Timestamp;

                        if (delta < _options.ProcessingTimeout)
                        {
                            // Возможно сообщение с этим ключем обрабатывается параллельно.
                            // Этот обработчик падает.
                            throw new Exception(
                                "Обнаружено необрабработанное сообщение с неистекшим timeout.");
                        }

                        // Обрабатываем.
                        // TODO: warning (возможно было обработано, но не подтверждено).
                        resultContext.NeedCommit.Add(elem);
                    }
                }

                if (forRemove.Any())
                {
                    foreach (var elem in groupByTriggerMessages)
                    {
                        for (var i = 0; i < elem.Value.Count; i++)
                        {
                            var elem2 = elem.Value[i];

                            if (forRemove.Contains(elem2.Message.Key))
                            {
                                elem.Value.RemoveAt(i);
                                i--;
                            }
                        }
                    }
                }
            }

            return resultContext;
        }

        public async ValueTask AfterCommitAsync(
            ITriggerEventInboxService.IContext context,
            CancellationToken cancellationToken)
        {
            if (context is not Context typedContext)
            {
                throw new ArgumentException(nameof(context));
            }

            var connection = await _redisConnectionFactory.GetAsync(_options.ConnectionName, cancellationToken);
            var db = connection.GetDatabase(_options.DatabaseId);

            var now = _dateTimeProvider.UtcNow;
            var expireTimeout = (now + _options.ExpireTimeout).UtcDateTime;
            var key = _options.HashKeyFactory();
            var sortedKey = _options.SortedSetKeyFactory();

            // Выставляем статус - обработан.

            switch (_options.ClearPolicy)
            {
                case OptionsDto.ClearPolicyEnum.ExpireDate:
                    {
                        var piplineTasks = new List<Task>(typedContext.NeedCommit.Count * 2);
                        foreach (var elem in typedContext.NeedCommit)
                        {
                            var t1 = db.HashSetAsync(key, elem.IsProcessedKey, 1, when: When.Always);
                            var t2 = db.HashFieldExpireAsync(key, [elem.IsProcessedKey], elem.BuildState().Value.Timestamp.UtcDateTime);
                            piplineTasks.Add(t1);
                            piplineTasks.Add(t2);
                        }

                        await connection.WaitPiplineWithTimeoutAsync(piplineTasks, cancellationToken);

                        break;
                    }

                case OptionsDto.ClearPolicyEnum.SizeLimit:
                    {
                        int setLenght;
                        {
                            var piplineTasks = new List<Task>(typedContext.NeedCommit.Count + 1);
                            foreach (var elem in typedContext.NeedCommit)
                            {
                                var t1 = db.HashSetAsync(key, elem.IsProcessedKey, 1, when: When.Always);
                                piplineTasks.Add(t1);
                            }

                            var lenghtTask = db.SortedSetLengthAsync(sortedKey);
                            piplineTasks.Add(lenghtTask);

                            await connection.WaitPiplineWithTimeoutAsync(piplineTasks, cancellationToken);

                            setLenght = (int)lenghtTask.Result;
                        }

                        // TODO: тут не уверен точно как лучше орагнизовать.
                        // В1: SortedSetRangeByScoreWithScoresAsync(skip: _options.SizeLimit) - Чтение O(n) на каждый вызов навеное не очень.
                        // В2: Проверка размера, чтение с начала избыточных. Тоже есть чтение, но не на каждый вызов.
                        if (setLenght > _options.SizeLimit)
                        {
                            var targerSize = (_options.SizeLimit / 2);

                            // Элементов нужно удалить.
                            var needRemoveCount = Math.Abs(targerSize - setLenght);

                            // Считываем набор для удаления.
                            var overLimit = await db.SortedSetRangeByScoreWithScoresAsync(sortedKey, order: Order.Ascending, take: needRemoveCount);

                            var isRemoved = await connection.ExecuteTransactionAsync(
                                (sortedKey, overLimit, targerSize),
                                db,
                                static (p, t) => 
                                {
                                    // Другая нода еще не удалила 1.
                                    var targerSizeResult = t.AddCondition(
                                        Condition.SortedSetLengthGreaterThan(p.sortedKey, p.targerSize));
                                    // Другая нода еще не удалила 2.
                                    var conditionResult = t.AddCondition(
                                        Condition.SortedSetContains(p.sortedKey, p.overLimit.First().Element));

                                    // Удаляем из SortedSet.
                                    var removeCommand = t.SortedSetRemoveAsync(
                                        p.sortedKey, 
                                        p.overLimit.Select(e => e.Element).ToArray());

                                    return 1;
                                },
                                static (p, c, r) => r
                                );

                            if (isRemoved)
                            {
                                // Если удаление было выполнено, то также удаляем из HashSet.
                                var removeKeys = new List<RedisValue>(overLimit.Length * 2);
                                foreach (var elem in overLimit)
                                {
                                    removeKeys.Add(_options.IsProcessedKeyFactory(elem.Element));
                                    removeKeys.Add(_options.StartProcessTimeStampKeyFactory(elem.Element));
                                }
                                await db.HashDeleteAsync(key, removeKeys.ToArray());
                            }
                        }
                        
                        break;
                    }

                default:
                    throw new NotImplementedException(
                        _options.ClearPolicy.ToString());
            }
        }

        public static string GetMessageKey(in MessageDto message)
        {
            return $"{message.Queue}.{message.Key}";
        }

        #region types

        public class OptionsDto
        {
            public required string ConnectionName { get; set; }

            public required int DatabaseId { get; set; }

            public Func<string> HashKeyFactory { get; set; }
                = static () => "trigger_event_inbox";

            public Func<string> SortedSetKeyFactory { get; set; }
                = static () => "sorted_event_inbox_sorted_keys";

            public Func<string, string> StartProcessTimeStampKeyFactory { get; set; }
                = static (e) => $"{e}_timestamp";

            public Func<string, string> IsProcessedKeyFactory { get; set; }
                = static (e) => $"{e}_is_processed";

            public required ClearPolicyEnum ClearPolicy { get; set; }

            public required int SizeLimit { get; set; }
                = 10000;

            public TimeSpan ExpireTimeout { get; set; }
                = TimeSpan.FromSeconds(30);

            public TimeSpan ProcessingTimeout { get; set; }
                = TimeSpan.FromSeconds(10);

            public enum ClearPolicyEnum
            {
                ExpireDate,
                SizeLimit
            }
        }

        private readonly record struct InboxEntry(
            long StartProcessTimeStamp,
            bool IsProcessed);

        private class Context 
            : ITriggerEventInboxService.IContext
        {
            public required List<ContextEntryDto> NeedCommit { get; set; }
        }

        private readonly record struct ContextEntryDto
        {
            // public required MessageDto Message { get; set; }

            public required string MessageKey { get; init; }

            public required string TimeStampKey { get; init; }

            public required string IsProcessedKey { get; init; }

            public Task<bool> InsertTask { get; init; }

            public Task<RedisValue> GetTimeStampTask { get; init; }

            public Task<RedisValue> GetIsProcessedTask { get; init; }

            public (bool IsInserted, bool IsProcessed, DateTimeOffset Timestamp)? BuildState()
            {
                return InsertTask is not null 
                    ? (
                        (bool)InsertTask.Result,
                        (int)GetIsProcessedTask.Result == 1,
                        new DateTimeOffset((long)GetTimeStampTask.Result, TimeSpan.Zero)
                        )
                    : null;
            }
        }

        #endregion
    }
}
