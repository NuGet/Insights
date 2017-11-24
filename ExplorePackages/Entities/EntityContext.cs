using System;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class EntityContext : DbContext
    {
        public static string ConnectionString { get; set; } = "Data Source=ExplorePackages.sqlite3";
        public static bool Enabled { get; set; } = true;

        public EntityContext()
        {
            if (!Enabled)
            {
                throw new NotSupportedException("Using the database is not enabled.");
            }
        }

        public DbSet<PackageRegistrationEntity> PackageRegistrations { get; set; }
        public DbSet<PackageEntity> Packages { get; set; }
        public DbSet<CursorEntity> Cursors { get; set; }
        public DbSet<PackageQueryEntity> PackageQueries { get; set; }
        public DbSet<PackageQueryMatchEntity> PackageQueryMatches { get; set; }
        public DbSet<V2PackageEntity> V2PackageEntities { get; set; }
        public DbSet<CatalogPackageEntity> CatalogPackageEntities { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlite(ConnectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
                .Entity<PackageRegistrationEntity>()
                .ToTable("PackageRegistrations");

            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .HasKey(x => x.PackageRegistrationKey);

            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .Property(x => x.Id)
                .HasColumnType("TEXT COLLATE NOCASE")
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
                .HasColumnType("TEXT COLLATE NOCASE")
                .IsRequired();

            modelBuilder
                .Entity<PackageEntity>()
                .Property(x => x.Identity)
                .HasColumnType("TEXT COLLATE NOCASE")
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
                .HasIndex(x => new { x.LastCommitTimestamp });

            modelBuilder
                .Entity<CatalogPackageEntity>()
                .HasOne(x => x.Package)
                .WithOne(x => x.CatalogPackage)
                .HasForeignKey<CatalogPackageEntity>(x => x.PackageKey);
        }
    }
}
