using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit.Abstractions;

namespace cccc1808.ProcessEngine.Test2.TestGroup1
{
    [CollectionDefinition(FixtureCollection.Name)]
    public class FixtureCollection : ICollectionFixture<FixtureCollection.Fixture>
    {
        public const string Name = "FixtureCollection";

        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.

        public class Fixture : IAsyncLifetime
        {
            public List<string> Action { get; }
                = new();

            public Task InitializeAsync()
            {
                // Выполняется один раз до запуска всех тестов, в которых используется.
                Action.Add("Fixture.InitializeAsync");
                return Task.CompletedTask;
            }

            public Task DisposeAsync()
            {
                // Выполняется один раз после запуска всех тестов, в которых используется.
                Action.Add("Fixture.DisposeAsync");
                return Task.CompletedTask;                
            }
        }
    }    
}
