using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxMaintenance<TDbContext>(
    TDbContext context)
    : IOutboxMaintenance
    where TDbContext : DbContext, IModuleDbContext
{
    public string ModuleName => context.ModuleName;

    public async Task<bool> RequeueDeadLetteredAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        OutboxMessage? message = await context.OutboxMessages
            .SingleOrDefaultAsync(
                message => message.MessageId == messageId,
                cancellationToken);

        if (message is null || message.Status != PersistedMessageStatus.DeadLettered)
        {
            return false;
        }

        message.RequeueDeadLettered();
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> DeleteProcessedBeforeAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        return context.OutboxMessages
            .Where(message =>
                message.Status == PersistedMessageStatus.Processed
                && message.DispatchedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
