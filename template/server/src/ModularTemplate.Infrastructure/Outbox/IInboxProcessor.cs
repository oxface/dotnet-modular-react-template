namespace ModularTemplate.Infrastructure.Outbox;

public interface IInboxProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken);
}
