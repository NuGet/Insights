using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Entities;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageRegistrationCommitEnumerator : ICommitEnumerator<PackageRegistrationEntity>
    {
        private readonly EntityContextFactory _entityContextFactory;

        public PackageRegistrationCommitEnumerator(
            EntityContextFactory entityContextFactory)
        {
            _entityContextFactory = entityContextFactory;
        }

        public async Task<IReadOnlyList<EntityCommit<PackageRegistrationEntity>>> GetCommitsAsync(
            DateTimeOffset start,
            DateTimeOffset end,
            int batchSize)
        {
            return await EnumeratorUtility.GetCommitsAsync(
                GetRangeAsync,
                x => x.CommitTimestamp,
                InitializePackageRegistrationCommit,
                start,
                end,
                batchSize);
        }

        private EntityCommit<PackageRegistrationEntity> InitializePackageRegistrationCommit(
            long commitTimestamp,
            IReadOnlyList<Record> records)
        {
            return new EntityCommit<PackageRegistrationEntity>(
                new DateTimeOffset(commitTimestamp, TimeSpan.Zero),
                records
                    .Select(x => new PackageRegistrationEntity
                    {
                        PackageRegistrationKey = x.PackageRegistrationKey,
                        Id = x.Id,
                    })
                    .ToList());
        }

        private async Task<IReadOnlyList<Record>> GetRangeAsync(
            long start,
            long end,
            int batchSize)
        {
            using (var entityContext = await _entityContextFactory.GetAsync())
            using (var connection = entityContext.Database.GetDbConnection())
            using (var command = connection.CreateCommand())
            {
                await connection.OpenAsync();

                command.CommandText = @"
                    SELECT pr.PackageRegistrationKey, pr.Id, MAX(cp.LastCommitTimestamp) AS CommitTimestamp
                    FROM PackageRegistrations pr
                    INNER JOIN Packages p ON pr.PackageRegistrationKey = p.PackageRegistrationKey
                    INNER JOIN CatalogPackages cp ON p.PackageKey = cp.PackageKey
                    WHERE cp.LastCommitTimestamp > @Start AND cp.LastCommitTimestamp <= @End
                    GROUP BY pr.PackageRegistrationKey
                    ORDER BY MAX(cp.LastCommitTimestamp) ASC
                    LIMIT @BatchSize";

                var startParameter = command.CreateParameter();
                startParameter.ParameterName = "Start";
                startParameter.DbType = DbType.Int64;
                startParameter.Value = start;
                command.Parameters.Add(startParameter);

                var endParameter = command.CreateParameter();
                endParameter.ParameterName = "End";
                endParameter.DbType = DbType.Int64;
                endParameter.Value = end;
                command.Parameters.Add(endParameter);

                var batchSizeParameter = command.CreateParameter();
                batchSizeParameter.ParameterName = "BatchSize";
                batchSizeParameter.DbType = DbType.Int32;
                batchSizeParameter.Value = batchSize;
                command.Parameters.Add(batchSizeParameter);

                var records = new List<Record>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var packageRegistrationKey = reader.GetInt64(0);
                        var id = reader.GetString(1);
                        var commitTimestamp = reader.GetInt64(2);

                        records.Add(new Record(
                            packageRegistrationKey,
                            id,
                            commitTimestamp));
                    }
                }

                return records;
            }
        }

        private class Record
        {
            public Record(long packageRegistrationKey, string id, long commitTimestamp)
            {
                PackageRegistrationKey = packageRegistrationKey;
                Id = id;
                CommitTimestamp = commitTimestamp;
            }

            public long PackageRegistrationKey { get; }
            public string Id { get; }
            public long CommitTimestamp { get; }
        }
    }
}
