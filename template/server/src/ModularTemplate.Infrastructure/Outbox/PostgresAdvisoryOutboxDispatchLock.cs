using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ModularTemplate.Infrastructure.Persistence;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class PostgresAdvisoryOutboxDispatchLock : IOutboxDispatchLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(
        IModuleDbContext dbContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        long lockKey = PostgresAdvisoryLockKey.ForModuleOutboxDispatch(dbContext.ModuleName);
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            object? result = await ExecuteScalarAsync(
                dbContext,
                "SELECT pg_try_advisory_lock(@lock_key)",
                lockKey,
                cancellationToken);

            if (result is bool acquired && acquired)
            {
                return new AdvisoryLockLease(dbContext, lockKey);
            }
        }
        catch
        {
            await dbContext.Database.CloseConnectionAsync();
            throw;
        }

        await dbContext.Database.CloseConnectionAsync();
        return null;
    }

    private static async Task ReleaseAsync(IModuleDbContext dbContext, long lockKey)
    {
        await ExecuteScalarAsync(
            dbContext,
            "SELECT pg_advisory_unlock(@lock_key)",
            lockKey,
            CancellationToken.None);
    }

    private static async Task<object?> ExecuteScalarAsync(
        IModuleDbContext dbContext,
        string commandText,
        long lockKey,
        CancellationToken cancellationToken)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;

        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "lock_key";
        parameter.Value = lockKey;
        command.Parameters.Add(parameter);

        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static class PostgresAdvisoryLockKey
    {
        public static long ForModuleOutboxDispatch(string moduleName)
        {
            string lockName = $"modular-template:outbox-dispatch:{moduleName}";

            // PostgreSQL advisory locks take numeric keys. Keep the source lock
            // name readable here and derive a deterministic 64-bit key from it.
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(lockName));
            return BitConverter.ToInt64(hash, 0);
        }
    }

    private sealed class AdvisoryLockLease(
        IModuleDbContext dbContext,
        long lockKey) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await ReleaseAsync(dbContext, lockKey);
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
