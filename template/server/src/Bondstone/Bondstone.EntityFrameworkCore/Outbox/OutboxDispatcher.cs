using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxDispatcher<TDbContext>(
    TDbContext context,
    IOutboxTransport outboxTransport,
    IOutboxDispatchLock outboxDispatchLock,
    IOptions<DurableMessagingOptions> options,
    IRetryDelayPolicy retryDelayPolicy,
    ILogger<OutboxDispatcher<TDbContext>> logger)
    : IOutboxDispatcher
    where TDbContext : DbContext, IModuleDbContext
{
    private readonly DurableMessagingOptions _options = options.Value;

    public string ModuleName => context.ModuleName;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await using IAsyncDisposable? dispatchLock = await outboxDispatchLock.TryAcquireAsync(context, cancellationToken);
        if (dispatchLock is null)
        {
            return 0;
        }

        return await DispatchLockedAsync(cancellationToken);
    }

    private async Task<int> DispatchLockedAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset staleThreshold = now - _options.LockTimeout;
        string claimToken = Guid.NewGuid().ToString("N");
        string schema = DelimitSchema(context.ModuleName);

        await MarkAbandonedProcessingMessagesAsync(
            schema,
            now,
            staleThreshold,
            cancellationToken);
        await ClaimEligibleMessagesAsync(
            schema,
            claimToken,
            now,
            cancellationToken);

        IReadOnlyList<OutboxMessage> pendingMessages = await context.OutboxMessages
            .Where(x => x.LockedBy == claimToken)
            .OrderBy(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        if (pendingMessages.Count == 0)
        {
            return 0;
        }

        int dispatchedCount = 0;

        foreach (OutboxMessage message in pendingMessages)
        {
            try
            {
                message.RefreshLock();
                await context.SaveChangesAsync(cancellationToken);
                await outboxTransport.DispatchAsync(message, cancellationToken);
                message.MarkProcessed();
                dispatchedCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Outbox dispatch failed for message {MessageId} type {MessageType}",
                    message.MessageId,
                    message.MessageType);
                message.MarkFailed(ex.Message, retryDelayPolicy.GetDelay);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        return dispatchedCount;
    }

    private async Task MarkAbandonedProcessingMessagesAsync(
        string schema,
        DateTimeOffset now,
        DateTimeOffset staleThreshold,
        CancellationToken cancellationToken)
    {
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

        await context.Database.ExecuteSqlAsync(
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

    private async Task ClaimEligibleMessagesAsync(
        string schema,
        string claimToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        string pending = PersistedMessageStatus.Pending.ToString();
        string failed = PersistedMessageStatus.Failed.ToString();

        // The subquery selects work in creation order, but FOR UPDATE SKIP LOCKED
        // lets concurrent dispatchers skip rows claimed by another transaction
        // instead of blocking.
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

        await context.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                claimSql,
                now,
                claimToken,
                pending,
                failed,
                now,
                _options.BatchSize),
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
