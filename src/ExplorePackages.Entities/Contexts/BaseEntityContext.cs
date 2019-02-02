using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public abstract class BaseEntityContext<T> : DbContext where T : DbContext, IEntityContext
    {
        private readonly ICommitCondition _commitCondition;

        public BaseEntityContext(
            ICommitCondition commitCondition, 
            DbContextOptions<T> options) : base(options)
        {
            _commitCondition = commitCondition ?? throw new ArgumentNullException(nameof(commitCondition));
        }

        public DbSet<LeaseEntity> Leases { get; set; }
        public DbSet<PackageRegistrationEntity> PackageRegistrations { get; set; }
        public DbSet<PackageEntity> Packages { get; set; }
        public DbSet<CursorEntity> Cursors { get; set; }
        public DbSet<ETagEntity> ETags { get; set; }
        public DbSet<PackageQueryEntity> PackageQueries { get; set; }
        public DbSet<PackageQueryMatchEntity> PackageQueryMatches { get; set; }
        public DbSet<V2PackageEntity> V2PackageEntities { get; set; }
        public DbSet<CatalogPackageEntity> CatalogPackages { get; set; }
        public DbSet<PackageDownloadsEntity> PackageDownloads { get; set; }
        public DbSet<PackageArchiveEntity> PackageArchives { get; set; }
        public DbSet<PackageEntryEntity> PackageEntries { get; set; }
        public DbSet<CatalogPageEntity> CatalogPages { get; set; }
        public DbSet<CatalogCommitEntity> CatalogCommits { get; set; }
        public DbSet<CatalogLeafEntity> CatalogLeaves { get; set; }
        public DbSet<FrameworkEntity> Frameworks { get; set; }
        public DbSet<PackageDependencyEntity> PackageDependencies { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _commitCondition.VerifyAsync();
            return await base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder
                .Entity<LeaseEntity>()
                .ToTable("Leases");
            modelBuilder
                .Entity<LeaseEntity>()
                .HasKey(x => x.LeaseKey);
            modelBuilder
                .Entity<LeaseEntity>()
                .Property(x => x.Name)
                .IsRequired();
            modelBuilder
                .Entity<LeaseEntity>()
                .Property(x => x.RowVersion)
                .IsRowVersion();
            modelBuilder
                .Entity<LeaseEntity>()
                .HasIndex(x => new { x.Name })
                .IsUnique();

            modelBuilder
                .Entity<CursorEntity>()
                .ToTable("Cursors");
            modelBuilder
                .Entity<CursorEntity>()
                .HasKey(x => x.CursorKey);
            modelBuilder
                .Entity<CursorEntity>()
                .Property(x => x.Name)
                .IsRequired();
            modelBuilder
                .Entity<CursorEntity>()
                .HasIndex(x => new { x.Name })
                .IsUnique();

            modelBuilder
                .Entity<ETagEntity>()
                .ToTable("ETags");
            modelBuilder
                .Entity<ETagEntity>()
                .HasKey(x => x.ETagKey);
            modelBuilder
                .Entity<ETagEntity>()
                .Property(x => x.Name)
                .IsRequired();
            modelBuilder
                .Entity<ETagEntity>()
                .HasIndex(x => new { x.Name })
                .IsUnique();

            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .ToTable("PackageRegistrations");
            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .HasKey(x => x.PackageRegistrationKey);
            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .Property(x => x.Id)
                .IsRequired();
            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .HasIndex(x => new { x.Id })
                .IsUnique();

            modelBuilder
                .Entity<PackageEntity>()
                .ToTable("Packages");
            modelBuilder
                .Entity<PackageEntity>()
                .HasKey(x => x.PackageKey);
            modelBuilder
                .Entity<PackageEntity>()
                .Property(x => x.Version)
                .IsRequired();
            modelBuilder
                .Entity<PackageEntity>()
                .Property(x => x.Identity)
                .IsRequired();
            modelBuilder
                .Entity<PackageEntity>()
                .HasIndex(x => new { x.Identity })
                .IsUnique();
            modelBuilder
                .Entity<PackageEntity>()
                .HasIndex(x => new { x.PackageRegistrationKey, x.Version })
                .IsUnique();
            modelBuilder
                .Entity<PackageEntity>()
                .Ignore(x => x.Id);

            modelBuilder
                .Entity<PackageQueryEntity>()
                .ToTable("PackageQueries");
            modelBuilder
                .Entity<PackageQueryEntity>()
                .Property(x => x.Name)
                .IsRequired();
            modelBuilder
                .Entity<PackageQueryEntity>()
                .HasKey(x => x.PackageQueryKey);            
            modelBuilder
                .Entity<PackageQueryEntity>()
                .HasIndex(x => new { x.Name })
                .IsUnique();
            modelBuilder
                .Entity<PackageQueryMatchEntity>()
                .ToTable("PackageQueryMatches");
            modelBuilder
                .Entity<PackageQueryMatchEntity>()
                .HasKey(x => x.PackageQueryMatchKey);            
            modelBuilder
                .Entity<PackageQueryMatchEntity>()
                .HasIndex(x => new { x.PackageQueryKey, x.PackageKey })
                .IsUnique();

            modelBuilder
                .Entity<V2PackageEntity>()
                .ToTable("V2Packages");
            modelBuilder
                .Entity<V2PackageEntity>()
                .HasKey(x => x.PackageKey);            
            modelBuilder
                .Entity<V2PackageEntity>()
                .HasIndex(x => new { x.CreatedTimestamp });
            modelBuilder
                .Entity<V2PackageEntity>()
                .HasIndex(x => new { x.LastEditedTimestamp });
            modelBuilder
                .Entity<V2PackageEntity>()
                .HasOne(x => x.Package)
                .WithOne(x => x.V2Package)
                .HasForeignKey<V2PackageEntity>(x => x.PackageKey);

            modelBuilder
                .Entity<CatalogPackageEntity>()
                .ToTable("CatalogPackages");
            modelBuilder
                .Entity<CatalogPackageEntity>()
                .HasKey(x => x.PackageKey);
            modelBuilder
                .Entity<CatalogPackageEntity>()
                .HasOne(x => x.Package)
                .WithOne(x => x.CatalogPackage)
                .HasForeignKey<CatalogPackageEntity>(x => x.PackageKey);

            modelBuilder
                .Entity<PackageDownloadsEntity>()
                .ToTable("PackageDownloads");
            modelBuilder
                .Entity<PackageDownloadsEntity>()
                .HasKey(x => x.PackageKey);
            modelBuilder
                .Entity<PackageDownloadsEntity>()
                .HasOne(x => x.Package)
                .WithOne(x => x.PackageDownloads)
                .HasForeignKey<PackageDownloadsEntity>(x => x.PackageKey);

            modelBuilder
                .Entity<PackageArchiveEntity>()
                .ToTable("PackageArchives");
            modelBuilder
                .Entity<PackageArchiveEntity>()
                .HasKey(x => x.PackageKey);
            modelBuilder
                .Entity<PackageArchiveEntity>()
                .HasOne(x => x.Package)
                .WithOne(x => x.PackageArchive)
                .HasForeignKey<PackageArchiveEntity>(x => x.PackageKey);
            modelBuilder
                .Entity<PackageArchiveEntity>()
                .Property(x => x.Comment)
                .IsRequired();

            modelBuilder
                .Entity<PackageEntryEntity>()
                .ToTable("PackageEntries");
            modelBuilder
                .Entity<PackageEntryEntity>()
                .HasKey(x => x.PackageEntryKey);
            modelBuilder
                .Entity<PackageEntryEntity>()
                .HasOne(x => x.PackageArchive)
                .WithMany(x => x.PackageEntries)
                .HasForeignKey(x => x.PackageKey);
            modelBuilder
                .Entity<PackageEntryEntity>()
                .Property(x => x.Comment)
                .IsRequired();
            modelBuilder
                .Entity<PackageEntryEntity>()
                .Property(x => x.ExtraField)
                .IsRequired();
            modelBuilder
                .Entity<PackageEntryEntity>()
                .Property(x => x.Name)
                .IsRequired();
            modelBuilder
                .Entity<PackageEntryEntity>()
                .HasIndex(x => new { x.PackageKey, x.Index })
                .IsUnique();

            modelBuilder
                .Entity<CatalogPageEntity>()
                .ToTable("CatalogPages");
            modelBuilder
                .Entity<CatalogPageEntity>()
                .HasKey(x => x.CatalogPageKey);
            modelBuilder
                .Entity<CatalogPageEntity>()
                .Property(x => x.Url)
                .IsRequired();
            modelBuilder
                .Entity<CatalogPageEntity>()
                .HasIndex(x => new { x.Url })
                .IsUnique();

            modelBuilder
                .Entity<CatalogCommitEntity>()
                .ToTable("CatalogCommits");
            modelBuilder
                .Entity<CatalogCommitEntity>()
                .HasKey(x => x.CatalogCommitKey);
            modelBuilder
                .Entity<CatalogCommitEntity>()
                .HasOne(x => x.CatalogPage)
                .WithMany(x => x.CatalogCommits)
                .HasForeignKey(x => x.CatalogPageKey);
            modelBuilder
                .Entity<CatalogCommitEntity>()
                .Property(x => x.CommitId)
                .IsRequired();
            modelBuilder
                .Entity<CatalogCommitEntity>()
                .HasIndex(x => new { x.CommitId })
                .IsUnique();
            modelBuilder
                .Entity<CatalogCommitEntity>()
                .HasIndex(x => new { x.CommitTimestamp })
                .IsUnique();

            modelBuilder
                .Entity<CatalogLeafEntity>()
                .ToTable("CatalogLeaves");
            modelBuilder
                .Entity<CatalogLeafEntity>()
                .HasKey(x => x.CatalogLeafKey);
            modelBuilder
                .Entity<CatalogLeafEntity>()
                .HasOne(x => x.CatalogCommit)
                .WithMany(x => x.CatalogLeaves)
                .HasForeignKey(x => x.CatalogCommitKey);
            modelBuilder
                .Entity<CatalogLeafEntity>()
                .HasOne(x => x.CatalogPackage)
                .WithMany(x => x.CatalogLeaves)
                .HasForeignKey(x => x.PackageKey);

            modelBuilder
                .Entity<FrameworkEntity>()
                .ToTable("Frameworks");
            modelBuilder
                .Entity<FrameworkEntity>()
                .HasKey(x => x.FrameworkKey);
            modelBuilder
                .Entity<FrameworkEntity>()
                .Property(x => x.Value)
                .IsRequired();
            modelBuilder
                .Entity<FrameworkEntity>()
                .Property(x => x.OriginalValue)
                .IsRequired();
            modelBuilder
                .Entity<FrameworkEntity>()
                .HasIndex(x => new { x.OriginalValue })
                .IsUnique();

            modelBuilder
                .Entity<PackageDependencyEntity>()
                .ToTable("PackageDependencies");
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasKey(x => x.PackageDependencyKey);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .Property(x => x.VersionRange);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .Property(x => x.OriginalVersionRange);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasOne(x => x.DependencyPackageRegistration)
                .WithMany(x => x.PackageDependents)
                .HasForeignKey(x => x.DependencyPackageRegistrationKey);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasOne(x => x.ParentPackage)
                .WithMany(x => x.PackageDependencies)
                .HasForeignKey(x => x.ParentPackageKey)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasOne(x => x.Framework)
                .WithMany(x => x.PackageDependencies)
                .HasForeignKey(x => x.FrameworkKey);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasIndex(x => new { x.ParentPackageKey, x.DependencyPackageRegistrationKey, x.FrameworkKey })
                .IsUnique();
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasOne(x => x.MinimumDependencyPackage)
                .WithMany(x => x.MinimumPackageDependents)
                .HasForeignKey(x => x.MinimumDependencyPackageKey);
            modelBuilder
                .Entity<PackageDependencyEntity>()
                .HasOne(x => x.BestDependencyPackage)
                .WithMany(x => x.BestPackageDependents)
                .HasForeignKey(x => x.BestDependencyPackageKey);
        }
    }
}
