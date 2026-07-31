namespace cccc1808.ProcessEngine.Model.Abstract.TriggerModule.Handlers
{
    public interface ITriggerHandler 
    {
        public readonly record struct ResultDto(
            bool NeedRemove,
            bool NeedRepeat,
            bool IsActivated,
            DateTimeOffset ExecuteTimeout)
        {
            public static ResultDto RemoveResult()
                => new ResultDto(NeedRemove: true, NeedRepeat: false, IsActivated: false, ExecuteTimeout: DateTimeOffset.MinValue);

            public static ResultDto ActivateResult(DateTimeOffset? executeTimeout = null)
                => new ResultDto(NeedRemove: false, NeedRepeat: true, IsActivated: true, ExecuteTimeout: executeTimeout ?? DateTimeOffset.MinValue);

            public static ResultDto NoActivateResult(DateTimeOffset? executeTimeout = null)
                => new ResultDto(NeedRemove: false, NeedRepeat: true, IsActivated: false, ExecuteTimeout: executeTimeout ?? DateTimeOffset.MinValue);
        }
    }
}
