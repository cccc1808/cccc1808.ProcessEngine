namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers
{
    public interface ITriggerHandler 
    {
        public readonly record struct Result(
            bool NeedRepeat,
            bool IsActivated,
            DateTimeOffset ExecuteDelay);
    }
}
