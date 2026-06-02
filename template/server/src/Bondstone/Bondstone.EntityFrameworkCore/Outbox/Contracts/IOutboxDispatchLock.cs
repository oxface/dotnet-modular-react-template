using Bondstone.EntityFrameworkCore.Persistence;

namespace Bondstone.EntityFrameworkCore.Outbox;

public interface IOutboxDispatchLock
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        IModuleDbContext dbContext,
        CancellationToken cancellationToken);
}
