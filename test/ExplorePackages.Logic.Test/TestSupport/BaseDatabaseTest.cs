using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Logic
{
    public abstract class BaseDatabaseTest : IAsyncLifetime
    {
        private readonly TestDirectory _testDirectory;

        public BaseDatabaseTest(ITestOutputHelper output)
        {
            Output = output;
            _testDirectory = TestDirectory.Create();
            DatabasePath = Path.Combine(_testDirectory, "Knapcode.ExplorePackages.sqlite3");
            EntityContextFactory = TestEntityContextFactory.Create(
                DatabasePath,
                output,
                async () =>
                {
                    var func = ExecuteBeforeCommitAsync;
                    if (func != null)
                    {
                        await func();
                    }
                });
            UnhookedEntityContextFactory = TestEntityContextFactory.Create(
                DatabasePath,
                output,
                () => Task.CompletedTask);
            ExecuteBeforeCommitAsync = () => Task.CompletedTask;
        }

        public ITestOutputHelper Output { get; }
        public string DatabasePath { get; }
        public TestEntityContextFactory EntityContextFactory { get; }
        public TestEntityContextFactory UnhookedEntityContextFactory { get; }
        public Func<Task> ExecuteBeforeCommitAsync { get; set; }

        public Task DisposeAsync()
        {
            _testDirectory.Dispose();
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            using (var entityContext = await EntityContextFactory.GetAsync())
            {
                var appliedMigrations = await entityContext.Database.GetAppliedMigrationsAsync();
                var migrations = entityContext.Database.GetMigrations();
                var pendingMigrations = await entityContext.Database.GetPendingMigrationsAsync();

                await entityContext.Database.MigrateAsync();
            }
        }
    }
}
