using Microsoft.EntityFrameworkCore;
using Npgsql;
using Bondstone.EntityFrameworkCore.Inbox;

namespace Bondstone.EntityFrameworkCore.Postgres.Inbox;

public sealed class PostgresInboxClaimConflictDetector : IInboxClaimConflictDetector
{
    public bool IsClaimConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
