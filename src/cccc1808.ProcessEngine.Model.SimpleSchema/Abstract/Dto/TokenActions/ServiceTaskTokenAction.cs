namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions
{
    /// <summary>
    /// Описывает выполнения действия.
    /// Содержит переход.
    /// </summary>
    public class ServiceTaskTokenAction 
        : BaseTokenAction
    {
        public string HandlerKey { get; set; }

        public ITokenAction.TransitionDto? Transition { get; set; }

        [Obsolete]
        public ServiceTaskTokenAction()
        {
            HandlerKey = default!;
        }

        public ServiceTaskTokenAction(
            string id,
            string handlerKey) 
            : base(
                id,
                null)
        {
            HandlerKey = handlerKey;
        }
    }
}
