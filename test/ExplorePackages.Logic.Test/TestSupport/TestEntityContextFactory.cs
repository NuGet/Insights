using System;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class TestEntityContextFactory : EntityContextFactory
    {
        private TestEntityContextFactory(Func<IEntityContext> getEntityContext) : base(getEntityContext)
        {
        }

        public static TestEntityContextFactory Create(string databasePath)
        {
            var builder = new DbContextOptionsBuilder<SqliteEntityContext>()
                .UseSqlite("Data Source=" + databasePath);

            Func<IEntityContext> getEntityContext = () => new SqliteEntityContext(builder.Options);

            return new TestEntityContextFactory(getEntityContext);
        }
    }
}
