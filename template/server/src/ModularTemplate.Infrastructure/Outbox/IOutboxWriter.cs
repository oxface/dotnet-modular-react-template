namespace ModularTemplate.Infrastructure.Outbox;

public interface IOutboxWriter
{
    void Write(OutboxMessage outboxMessage);
}
