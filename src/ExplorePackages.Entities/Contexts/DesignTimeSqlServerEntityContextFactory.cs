using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Knapcode.ExplorePackages.Entities
{
    public class DesignTimeSqlServerEntityContextFactory : IDesignTimeDbContextFactory<SqlServerEntityContext>
    {
        public SqlServerEntityContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqlServerEntityContext>();
            optionsBuilder.UseSqlServer(
                "Data Source=(localdb)\\mssqllocaldb; " +
                "Initial Catalog=Knapcode.ExplorePackages; " +
                "Integrated Security=True; " +
                "MultipleActiveResultSets=True");
            return new SqlServerEntityContext(
                NullCommitCondition.Instance,
                optionsBuilder.Options);
        }
    }
}
