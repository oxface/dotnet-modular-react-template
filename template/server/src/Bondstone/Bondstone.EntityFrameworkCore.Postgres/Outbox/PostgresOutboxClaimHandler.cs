using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Postgres.Outbox;

public sealed class PostgresOutboxClaimHandler : IOutboxClaimHandler
{
    public async Task MarkAbandonedProcessingMessagesAsync(
        IModuleDbContext dbContext,
        DateTimeOffset now,
        DateTimeOffset staleThreshold,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        string schema = DelimitSchema(dbContext.ModuleName);
        string failed = PersistedMessageStatus.Failed.ToString();
        string processing = PersistedMessageStatus.Processing.ToString();
        string deadLettered = PersistedMessageStatus.DeadLettered.ToString();
        DateTimeOffset neverRetry = DateTimeOffset.MaxValue;

        string staleSql =
            $$"""
            UPDATE {{schema}}.outbox_messages
            SET "AttemptCount" = "AttemptCount" + 1,
                "Status" = CASE
                    WHEN "AttemptCount" + 1 >= "MaxAttempts" THEN {0}
                    ELSE {1}
                END,
                "FailedAtUtc" = {2},
                "Error" = 'Outbox message lock timed out before dispatch completed.',
                "LockedAtUtc" = NULL,
                "LockedBy" = NULL,
                "NextAttemptAtUtc" = CASE
                    WHEN "AttemptCount" + 1 >= "MaxAttempts" THEN {3}
                    ELSE {2}
                END
            WHERE "Status" = {4}
                AND "LockedAtUtc" IS NOT NULL
                AND "LockedAtUtc" < {5}
            """;

        await dbContext.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                staleSql,
                deadLettered,
                failed,
                now,
                neverRetry,
                processing,
                staleThreshold),
            cancellationToken);
    }

    public async Task ClaimEligibleMessagesAsync(
        IModuleDbContext dbContext,
        string claimToken,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimToken);

        string schema = DelimitSchema(dbContext.ModuleName);
        string pending = PersistedMessageStatus.Pending.ToString();
        string failed = PersistedMessageStatus.Failed.ToString();

        string claimSql =
            $$"""
            UPDATE {{schema}}.outbox_messages
            SET "Status" = 'Processing',
                "LockedAtUtc" = {0},
                "LockedBy" = {1}
            WHERE "Id" = ANY(
                SELECT "Id" FROM {{schema}}.outbox_messages
                WHERE (
                    ("Status" = {2} OR "Status" = {3})
                    AND "NextAttemptAtUtc" <= {4}
                )
                ORDER BY "CreatedAtUtc"
                LIMIT {5}
                FOR UPDATE SKIP LOCKED
            )
            """;

        await dbContext.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                claimSql,
                now,
                claimToken,
                pending,
                failed,
                now,
                batchSize),
            cancellationToken);
    }

    private static string DelimitSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema)
            || schema.Any(static c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new InvalidOperationException($"Invalid module schema name '{schema}'.");
        }

        return "\"" + schema + "\"";
    }
}
