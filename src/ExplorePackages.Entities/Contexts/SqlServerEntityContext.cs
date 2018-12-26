using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Knapcode.ExplorePackages.Entities
{
    public class SqlServerEntityContext : BaseEntityContext<SqlServerEntityContext>, IEntityContext
    {
        public SqlServerEntityContext(DbContextOptions<SqlServerEntityContext> options) : base(options)
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
    }
}
