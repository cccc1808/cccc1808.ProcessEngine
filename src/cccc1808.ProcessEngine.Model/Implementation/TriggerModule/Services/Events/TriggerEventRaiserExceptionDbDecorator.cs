using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Services.Events;

namespace cccc1808.ProcessEngine.Model.Implementation.TriggerModule.Services.Events
{
    public class TriggerEventRaiserExceptionDbDecorator<TId>
        : ITriggerEventRaiser<TId>
    {
        private readonly ITriggerEventRaiser<TId> _source;
        private readonly IQuery _query;

        public TriggerEventRaiserExceptionDbDecorator(
            ITriggerEventRaiser<TId> source, 
            IQuery query)
        {
            _source = source;
            _query = query;
        }

        public async ValueTask RaiseAsync(
            ICollection<ITriggerEventRaiser<TId>.RaiseContainer> events,
            CancellationToken cancellationToken)
        {
            try
            {
                await _source.RaiseAsync(events, cancellationToken);
            }
            catch(Exception ex)
            {
                // Не удалось отправть событие в очередь напрямую, сохранием его в БД outbox.
                // TODO: log;

                try
                {
                    await _query.SaveToDbOutboxAsync(events, cancellationToken);
                }
                catch (Exception ex2) 
                {
                    // Событие потеряно.
                    // TODO: log;
                }                                
            }
        }

        public void ClearBuffer()
        {
            _source.ClearBuffer();
        }

        public interface IQuery
        {
            Task SaveToDbOutboxAsync(
                ICollection<ITriggerEventRaiser<TId>.RaiseContainer> events,           
                CancellationToken cancellationToken);

            Task<ICollection<ITriggerEventRaiser<TId>.RaiseContainer>> LoadForSendAsync(
                int batchSize, 
                CancellationToken cancellationToken);
        }
    }
}
