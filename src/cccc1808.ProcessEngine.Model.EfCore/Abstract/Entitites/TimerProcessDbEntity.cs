namespace cccc1808.ProcessEngine.Model.EfCore.Abstract.Entitites
{
    public class TimerProcessDbEntity<TId> 
        : ProcessDbEntity<TId>
    {
        public TId? LinkedProcessId { get; set; }
        /// <summary>
        /// True - Process, False - Timer.
        /// </summary>
        public bool IsProcessOrTimer { get; set; }
        public ProcessDbEntity<TId>? LinkedProcess { get; set; }
        public DateTimeOffset TimerDate { get; set; }
    }
}
