namespace ModularTemplate.Infrastructure.Outbox;

public interface IOutboxWriter
{
    string ModuleName { get; }

    void Write(OutboxMessage outboxMessage);
}
