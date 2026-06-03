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
    IOutboxClaimHandler outboxClaimHandler,
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

        await outboxClaimHandler.MarkAbandonedProcessingMessagesAsync(
            context,
            now,
            staleThreshold,
            cancellationToken);
        await outboxClaimHandler.ClaimEligibleMessagesAsync(
            context,
            claimToken,
            now,
            _options.BatchSize,
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
}
