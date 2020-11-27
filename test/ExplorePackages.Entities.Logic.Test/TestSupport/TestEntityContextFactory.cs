using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Knapcode.ExplorePackages.Entities
{
    public class TestEntityContextFactory : EntityContextFactory
    {
        private TestEntityContextFactory(Func<IEntityContext> getEntityContext) : base(getEntityContext)
        {
        }

        public static TestEntityContextFactory Create(
            string databasePath,
            ITestOutputHelper output,
            Func<Task> executeBeforeCommitAsync)
        {
            var loggerFactory = LoggerFactory
                .Create(b => b.SetMinimumLevel(LogLevel.Warning))
                .AddXunit(output);

            var builder = new DbContextOptionsBuilder<SqliteEntityContext>()
                .UseLoggerFactory(loggerFactory)
                .UseSqlite(
                    "Data Source=" + databasePath,
                    o => o.MigrationsAssembly(typeof(SqliteEntityContext).Assembly.FullName));

            Func<IEntityContext> getEntityContext = () => new TestSqliteEntityContext(
                new SqliteEntityContext(
                    NullCommitCondition.Instance,
                    builder.Options),
                builder.Options,
                executeBeforeCommitAsync);

            return new TestEntityContextFactory(getEntityContext);
        }
    }
}
