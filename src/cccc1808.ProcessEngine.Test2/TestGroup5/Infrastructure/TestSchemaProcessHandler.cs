using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Components;
using cccc1808.ProcessEngine.Model.Abstract.ProcessModule.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Abstract.Dto.TokenActions;
using cccc1808.ProcessEngine.Model.SimpleSchema.EF.Implementation.Handlers;

namespace cccc1808.ProcessEngine.Test2.TestGroup5.Infrastructure
{
    internal class TestSchemaProcessHandler : BaseSchemaProcessHandler<Guid>
    {
        public TestSchemaProcessHandler() : 
            base()
        {
            RegistryServiceTask("1", TestActionAsync);
        }

        private ValueTask<bool> TestActionAsync(
            string name,
            string actionId,
            IProcessContainer<Guid> process,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(true);
        }

        public static string Key { get; }
            = "1";

        public static ProcessSchemaDto Schema { get; }
            = new ProcessSchemaDto(
                "1",
                new Dictionary<string, TokenDto>() 
                {
                    ["1"] = new TokenDto("1", [new ServiceTaskTokenAction("1", "1", ITokenAction.TransitionDto.Target("2"))]),
                    ["2"] = new TokenDto("2", [new TimerTokenAction("1", ITokenAction.TransitionDto.Complete(), null, TimeSpan.Zero)]),
                }
                );

        public static ProcessTypeDto ProcessType { get; }
            = new ProcessTypeDto(1, 1);
    }
}
