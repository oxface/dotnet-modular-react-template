using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxDispatcher(
    IEnumerable<ModulePersistenceRegistration> persistenceRegistrations,
    IModulePersistenceResolver persistenceResolver,
    IOutboxTransport outboxTransport,
    IOutboxDispatchLock outboxDispatchLock,
    IOptions<DurableMessagingOptions> options,
    IRetryDelayPolicy retryDelayPolicy,
    ILogger<OutboxDispatcher> logger)
    : IOutboxDispatcher
{
    private readonly DurableMessagingOptions _options = options.Value;

    public async Task<int> DispatchPendingAsync(CancellationToken cancellationToken)
    {
        int totalDispatched = 0;

        foreach (string moduleName in GetRegisteredModuleNames())
        {
            try
            {
                IModuleDbContext ctx = persistenceResolver.ResolveDbContext(moduleName);
                totalDispatched += await DispatchForContextAsync(ctx, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Outbox dispatch failed for module {ModuleName}",
                    moduleName);
            }
        }

        return totalDispatched;
    }

    private string[] GetRegisteredModuleNames()
    {
        return persistenceRegistrations
            .Select(registration => registration.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<int> DispatchForContextAsync(
        IModuleDbContext ctx,
        CancellationToken cancellationToken)
    {
        await using IAsyncDisposable? dispatchLock = await outboxDispatchLock.TryAcquireAsync(ctx, cancellationToken);
        if (dispatchLock is null)
        {
            return 0;
        }

        return await DispatchLockedForContextAsync(ctx, cancellationToken);
    }

    private async Task<int> DispatchLockedForContextAsync(
        IModuleDbContext ctx,
        CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset staleThreshold = now - _options.LockTimeout;
        string claimToken = Guid.NewGuid().ToString("N");
        string schema = DelimitSchema(ctx.ModuleName);

        await ClaimEligibleMessagesAsync(
            ctx,
            schema,
            claimToken,
            now,
            staleThreshold,
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
                message.RefreshLock();
                await ctx.SaveChangesAsync(cancellationToken);
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

            await ctx.SaveChangesAsync(cancellationToken);
        }

        return dispatchedCount;
    }

    private async Task ClaimEligibleMessagesAsync(
        IModuleDbContext ctx,
        string schema,
        string claimToken,
        DateTimeOffset now,
        DateTimeOffset staleThreshold,
        CancellationToken cancellationToken)
    {
        string pending = PersistedMessageStatus.Pending.ToString();
        string failed = PersistedMessageStatus.Failed.ToString();
        string processing = PersistedMessageStatus.Processing.ToString();

        // The subquery selects work in creation order, but FOR UPDATE SKIP LOCKED
        // lets concurrent dispatchers skip rows claimed by another transaction
        // instead of blocking. Processing rows older than LockTimeout are treated
        // as abandoned and become claimable again after a host crash or timeout.
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
