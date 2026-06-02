namespace Bondstone.Messaging;

public interface IOutboxTransport
{
    Task DispatchAsync(IDurableOutboxMessage outboxMessage, CancellationToken cancellationToken);
}
