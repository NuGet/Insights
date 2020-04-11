using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class SqlServerEntityContext : BaseEntityContext<SqlServerEntityContext>, IEntityContext
    {
        public SqlServerEntityContext(
            ICommitCondition commitCondition,
            DbContextOptions<SqlServerEntityContext> options) : base(commitCondition, options)
        {
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/6483854
        /// </summary>
        public bool IsUniqueConstraintViolationException(Exception exception)
        {
            var baseException = exception.GetBaseException();
            if (baseException is SqlException sqlException)
            {
                return sqlException
                    .Errors
                    .Cast<SqlError>()
                    .Any(x => x.Number == 2627);
            }

            return false;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasIndex(x => x.ParentPackageKey)
                .IncludeProperties(x => new
                {
                    x.BestDependencyPackageKey,
                    x.DependencyPackageRegistrationKey,
                    x.FrameworkKey,
                    x.MinimumDependencyPackageKey,
                    x.OriginalVersionRange,
                    x.VersionRange,
                });

            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasIndex(x => new { x.DependencyPackageRegistrationKey, x.PackageDependencyKey })
                .IncludeProperties(x => new
                {
                    x.BestDependencyPackageKey,
                    x.FrameworkKey,
                    x.MinimumDependencyPackageKey,
                    x.OriginalVersionRange,
                    x.ParentPackageKey,
                    x.VersionRange,
                });

            modelBuilder
                .Entity<CatalogPackageEntity>()
                .HasIndex(x => x.LastCommitTimestamp)
                .IncludeProperties(x => new
                {
                    x.Deleted,
                    x.FirstCommitTimestamp,
                    x.Listed,
                    x.SemVerType,
                });

            modelBuilder
                .Entity<CatalogPackageRegistrationEntity>()
                .HasIndex(x => x.LastCommitTimestamp)
                .IncludeProperties(x => new
                {
                    x.FirstCommitTimestamp,
                });
        }
    }
}
