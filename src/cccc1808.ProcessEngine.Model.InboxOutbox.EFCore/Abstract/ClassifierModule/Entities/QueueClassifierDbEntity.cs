using cccc1808.ProcessEngine.Model.Abstract.CommonModule.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.ClassifierModule.Entities
{
    /// <summary>
    /// Справочник очередей.
    /// </summary>
    /// <typeparam name="TId"></typeparam>
    public class QueueClassifierDbEntity<TId>
        : IId<TId>
    {
        public TId Id { get; set; } = default!;

        public string Name { get; set; } = default!;


        public QueueClassifierDbEntity() 
        {
        }

        public QueueClassifierDbEntity(TId id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
