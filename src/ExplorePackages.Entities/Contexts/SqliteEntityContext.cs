using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Knapcode.ExplorePackages.Entities
{
    public class SqliteEntityContext : BaseEntityContext<SqliteEntityContext>, IEntityContext
    {
        public SqliteEntityContext(
            ICommitCondition commitCondition,
            DbContextOptions<SqliteEntityContext> options) : base(commitCondition, options)
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

            // Source: https://stackoverflow.com/a/52738603
            var timestampProperties = modelBuilder
                .Model
                .GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(byte[])
                    && p.ValueGenerated == ValueGenerated.OnAddOrUpdate
                    && p.IsConcurrencyToken);
            foreach (var property in timestampProperties)
            {
                property.SetValueConverter(new SqliteTimestampConverter());
            }
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/52738603
        /// </summary>
        private class SqliteTimestampConverter : ValueConverter<byte[], string>
        {
            public SqliteTimestampConverter() : base(
                v => v == null ? null : ToDb(v),
                v => v == null ? null : FromDb(v))
            {
            }

            private static byte[] FromDb(string v)
            {
                return v.Select(c => (byte)c).ToArray();
            }

            private static string ToDb(byte[] v)
            {
                return new string(v.Select(b => (char)b).ToArray());
            }
        }
    }
}
