using System;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class TestEntityContextFactory : EntityContextFactory
    {
        private TestEntityContextFactory(Func<IEntityContext> getEntityContext) : base(getEntityContext)
        {
        }

        public static TestEntityContextFactory Create(
            string databasePath,
            Func<Task> executeBeforeCommitAsync)
        {
            var builder = new DbContextOptionsBuilder<SqliteEntityContext>()
                .UseSqlite(
                    "Data Source=" + databasePath,
                    o => o.MigrationsAssembly(typeof(SqliteEntityContext).Assembly.FullName));

            Func<IEntityContext> getEntityContext = () => new TestSqliteEntityContext(
                new SqliteEntityContext(builder.Options),
                builder.Options,
                executeBeforeCommitAsync);

            return new TestEntityContextFactory(getEntityContext);
        }
    }
}
