using cccc1808.ProcessEngine.Model.Common.Entities;

namespace cccc1808.ProcessEngine.Model.InboxOutbox.EFCore.Abstract.Entities.Classifiers
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
    }
}
