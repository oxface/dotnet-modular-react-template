namespace ModularTemplate.Outbox;

public interface IInboxProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken);
}
