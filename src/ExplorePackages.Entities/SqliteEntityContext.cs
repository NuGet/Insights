using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class SqliteEntityContext : BaseEntityContext<SqliteEntityContext>, IEntityContext
    {
        public SqliteEntityContext(DbContextOptions<SqliteEntityContext> options) : base(options)
        {
        }

        public async Task BackupDatabaseAsync(string destinationDataSource)
        {
            var builder = new SqliteConnectionStringBuilder();
            builder.DataSource = destinationDataSource;
            var connectionString = builder.ConnectionString;

            using (var sourceConnection = (SqliteConnection)Database.GetDbConnection())
            using (var destinationConnection = new SqliteConnection(connectionString))
            {
                await sourceConnection.OpenAsync();
                await destinationConnection.OpenAsync();
                sourceConnection.BackupDatabase(destinationConnection);
            }
        }

        /// <summary>
        /// Source: https://www.sqlite.org/c3ref/c_abort.html
        /// </summary>
        public bool IsUniqueConstraintViolationException(Exception exception)
        {
            var baseException = exception.GetBaseException();
            if (baseException is SqliteException sqliteException)
            {
                return sqliteException.SqliteErrorCode == 19;
            }

            return false;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<PackageRegistrationEntity>()
                .Property(x => x.Id)
                .HasColumnType("TEXT COLLATE NOCASE");

            modelBuilder
                .Entity<PackageEntity>()
                .Property(x => x.Version)
                .HasColumnType("TEXT COLLATE NOCASE");

            modelBuilder
                .Entity<PackageEntity>()
                .Property(x => x.Identity)
                .HasColumnType("TEXT COLLATE NOCASE");
        }
    }
}
