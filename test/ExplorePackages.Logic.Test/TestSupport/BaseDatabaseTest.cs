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
        public BaseDatabaseTest(ITestOutputHelper output)
        {
            Output = output;
            DatabasePath = Path.GetTempFileName();
            EntityContextFactory = TestEntityContextFactory.Create(
                DatabasePath,
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
            if (File.Exists(DatabasePath))
            {
                File.Delete(DatabasePath);
            }

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
