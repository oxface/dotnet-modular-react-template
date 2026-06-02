namespace Bondstone.EntityFrameworkCore.Outbox;

public interface IOutboxDispatcher
{
    string ModuleName { get; }

    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
