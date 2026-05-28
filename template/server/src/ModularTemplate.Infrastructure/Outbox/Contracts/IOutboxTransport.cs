namespace ModularTemplate.Infrastructure.Outbox;

public interface IOutboxTransport
{
    Task DispatchAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken);
}
