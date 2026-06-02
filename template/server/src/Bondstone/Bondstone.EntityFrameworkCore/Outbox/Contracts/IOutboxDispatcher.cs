namespace Bondstone.EntityFrameworkCore.Outbox;

public interface IOutboxDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
