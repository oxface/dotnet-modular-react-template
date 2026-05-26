namespace ModularTemplate.Outbox;

public interface IOutboxWriter
{
    void Write(OutboxMessage outboxMessage);
}
