using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.Abstract.Component.Dto.TokenActions
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

        /// <summary>
        /// TODO: возможно сделать так же как и CanRunAction,
        /// чтобы можно было выбирать один из (в зависимости от условия).
        /// А на схеме отображать через декларирование.
        /// </summary>
        public ITokenAction.TransitionDto? Transition { get; set; }


        [Obsolete]
        public ConditionTokenAction()
        {
            CheckHandlerKey = default!;
        }

        public ConditionTokenAction(
            string id,
            string checkHandlerKey)
            : base(
                  id,
                  null)
        {
            CheckHandlerKey = checkHandlerKey;
        }        
    }
}
