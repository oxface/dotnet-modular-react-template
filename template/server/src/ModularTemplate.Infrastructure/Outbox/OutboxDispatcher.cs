using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class OutboxDispatcher(
    IEnumerable<IModuleDbContext> moduleContexts,
    IOutboxTransport outboxTransport,
    ILocalSubscriptionRegistry subscriptionRegistry,
    IOptions<DurableMessagingOptions> options,
    ILogger<OutboxDispatcher> logger)
    : IOutboxDispatcher
{
    private readonly DurableMessagingOptions _options = options.Value;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        int totalDispatched = 0;

        foreach (IModuleDbContext ctx in moduleContexts)
        {
            totalDispatched += await DispatchForContextAsync(ctx, cancellationToken);
        }

        return totalDispatched;
    }

    private async Task<int> DispatchForContextAsync(
        IModuleDbContext ctx,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset staleThreshold = now - _options.LockTimeout;
        string claimToken = Guid.NewGuid().ToString("N");
        string schema = DelimitSchema(ctx.ModuleName);

        string pending = PersistedMessageStatus.Pending.ToString();
        string failed = PersistedMessageStatus.Failed.ToString();
        string processing = PersistedMessageStatus.Processing.ToString();

        // Atomically claim a batch using FOR UPDATE SKIP LOCKED to prevent double-processing
        // under concurrent workers. Stale Processing rows (lock expired) are re-claimable.
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
                ) OR (
                    "Status" = {5}
                    AND "LockedAtUtc" IS NOT NULL
                    AND "LockedAtUtc" < {6}
                )
                ORDER BY "CreatedAtUtc"
                LIMIT {7}
                FOR UPDATE SKIP LOCKED
            )
            """;

        await ctx.Database.ExecuteSqlAsync(
            FormattableStringFactory.Create(
                claimSql,
                now,
                claimToken,
                pending,
                failed,
                now,
                processing,
                staleThreshold,
                _options.BatchSize),
            cancellationToken);

        IReadOnlyList<OutboxMessage> pendingMessages = await ctx.OutboxMessages
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
                if (message.MessageKind == MessageKind.Event)
                {
                    IReadOnlyList<string> subscribers =
                        subscriptionRegistry.GetEventSubscribers(message.MessageType);

                    if (subscribers.Count == 0)
                    {
                        logger.LogDebug(
                            "Outbox event {MessageType} has no subscribers; marking processed",
                            message.MessageType);
                        message.MarkProcessed();
                        dispatchedCount++;
                        continue;
                    }

                    foreach (string subscriber in subscribers)
                    {
                        await outboxTransport.DispatchAsync(message, subscriber, cancellationToken);
                    }

                    message.MarkProcessed();
                    dispatchedCount++;
                    continue;
                }

                // Command: TargetModule is required on the message itself.
                if (string.IsNullOrWhiteSpace(message.TargetModule))
                {
                    throw new InvalidOperationException(
                        $"Outbox command message '{message.Id}' (type '{message.MessageType}') requires TargetModule.");
                }

                await outboxTransport.DispatchAsync(message, message.TargetModule, cancellationToken);
                message.MarkProcessed();
                dispatchedCount++;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Outbox dispatch failed for message {MessageId} type {MessageType}",
                    message.MessageId,
                    message.MessageType);
                message.MarkFailed(ex.Message, RetryDelays.ForAttempt);
            }
        }

        await ctx.SaveChangesAsync(cancellationToken);
        return dispatchedCount;
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
