namespace ModularTemplate.Infrastructure.Outbox;

public interface IOutboxDispatcher
{
    Task<int> DispatchPendingAsync(CancellationToken cancellationToken);
}
