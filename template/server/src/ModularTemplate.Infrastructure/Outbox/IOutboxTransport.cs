namespace ModularTemplate.Infrastructure.Outbox;

public interface IOutboxTransport
{
    Task DispatchAsync(OutboxMessage outboxMessage, string targetModule, CancellationToken cancellationToken);
}
