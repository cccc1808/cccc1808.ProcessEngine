using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit.Abstractions;

namespace cccc1808.ProcessEngine.Test2.TestGroup1
{
    [Collection(FixtureCollection.Name)]
    public class Test2
        : IAsyncLifetime
    {
        private readonly FixtureCollection.Fixture _fixture;
        private readonly ITestOutputHelper _testOutputHelper;


        public Test2(
            FixtureCollection.Fixture fixture,
            ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _testOutputHelper = testOutputHelper;
        }

        public Task InitializeAsync()
        {
            _testOutputHelper.WriteLine("Test2.InitializeAsync");
            _fixture.Action.Add("Test2.InitializeAsync");
            return Task.CompletedTask;
        }

        [Fact]
        public void T1()
        {
            _testOutputHelper.WriteLine("Test2.T1");
            _fixture.Action.Add("Test2.T1");
        }

        [Fact]
        public void T2()
        {
            _testOutputHelper.WriteLine("Test2.T2");
            _fixture.Action.Add("Test2.T2");
        }

        public Task DisposeAsync()
        {
            _testOutputHelper.WriteLine("Test2.DisposeAsync");
            _fixture.Action.Add("Test2.DisposeAsync");
            return Task.CompletedTask;
        }
    }
}
