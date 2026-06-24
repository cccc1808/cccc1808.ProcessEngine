using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions
{
    /// <summary>
    /// Действие, прикрепляемое к токену процесса.
    /// </summary>
    public interface ITokenAction
    {
        string Id { get; }

        string? Name { get; }

        string? Description { get; }

        /// <summary>
        /// Действие активируется в начале выполнения токена.
        /// </summary>
        bool ActivatedOnStart { get; }

        RunActionDeclarationDto[] CanRunAction { get; }

        /// <summary>
        /// Указывает переход при обрабокте действия.
        /// </summary>
        /// <param name="TargetTokenId">Идентефикатор токена на который нужно выполнить переход.</param>
        /// <param name="IsComplete">Указывает, что процесс завершен.</param>
        public readonly record struct TransitionDto(
            string? TargetTokenId,
            bool IsComplete,
            string? Comment = null
            )
        {
            public static TransitionDto Target(
                string targetTokenid,
                string? comment = null)
                => new TransitionDto(targetTokenid, IsComplete: false, comment);

            public static TransitionDto Complete()
                => new TransitionDto(null, IsComplete: true, null);
        }

        public readonly record struct RunActionDeclarationDto(
            string ActivateActionId,
            string? Comment = null);
    }
}
