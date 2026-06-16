using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions
{
    /// <summary>
    /// Описывает проверку условия 
    /// (предпологается ожидание и поступление сигнала).
    /// (допускает  предварительнео создание триггера).
    /// </summary>
    public class ConditionTokenAction 
        : BaseTokenAction
    {       
        public string CheckHandlerKey { get; set; }

        public string? ActionHandlerKey { get; set; }

        public ITokenAction.TransitionDto? Transition { get; set; }


        [Obsolete]
        public ConditionTokenAction()
        {
            CheckHandlerKey = default!;
        }

        public ConditionTokenAction(
            string id,
            string checkHandlerKey,
            string? actionHandlerKey,
            ITokenAction.TransitionDto? transition)
            : base(
                  id)
        {
            CheckHandlerKey = checkHandlerKey;
            ActionHandlerKey = actionHandlerKey;
            Transition = transition;
        }
    }
}
