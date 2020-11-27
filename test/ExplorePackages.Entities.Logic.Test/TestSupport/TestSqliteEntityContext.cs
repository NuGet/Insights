using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Knapcode.ExplorePackages.Entities
{
    public class TestSqliteEntityContext : SqliteEntityContext
    {
        private readonly SqliteEntityContext _inner;
        private readonly Func<Task> _executeBeforeCommitAsync;

        public TestSqliteEntityContext(
            SqliteEntityContext inner,
            DbContextOptions<SqliteEntityContext> options,
            Func<Task> executeBeforeCommitAsync) : base(
                NullCommitCondition.Instance,
                options)
        {
            _inner = inner;
            _executeBeforeCommitAsync = executeBeforeCommitAsync;
        }

        public override DatabaseFacade Database => _inner.Database;

        public override void Dispose()
        {
            _inner.Dispose();
            base.Dispose();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _executeBeforeCommitAsync();

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
