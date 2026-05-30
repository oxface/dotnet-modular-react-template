using ModularTemplate.Infrastructure.Persistence;

namespace ModularTemplate.Infrastructure.Outbox;

public interface IOutboxDispatchLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        IModuleDbContext dbContext,
        CancellationToken cancellationToken);
}
