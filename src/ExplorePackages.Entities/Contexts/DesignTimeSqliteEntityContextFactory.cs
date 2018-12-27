using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Knapcode.ExplorePackages.Entities
{
    public class DesignTimeSqliteEntityContextFactory : IDesignTimeDbContextFactory<SqliteEntityContext>
    {
        public SqliteEntityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqliteEntityContext>();
            optionsBuilder.UseSqlite(
                "Data Source=Knapcode.ExplorePackages.sqlite3");
            return new SqliteEntityContext(
                NullCommitCondition.Instance,
                optionsBuilder.Options);
        }
    }
}
