namespace Bondstone.EntityFrameworkCore.Persistence.DomainEvents;

public sealed class StoredDomainEvent
{
    internal StoredDomainEvent(
        Guid id, DateTimeOffset occurredAt, string aggregateType, string aggregateId,
        string eventType, int eventVersion, string payload, string? metadata)
    {
        Id = id; OccurredAt = occurredAt; AggregateType = aggregateType;
        AggregateId = aggregateId; EventType = eventType; EventVersion = eventVersion;
        Payload = payload; Metadata = metadata;
    }

    private StoredDomainEvent() { }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string AggregateType { get; private set; } = string.Empty;
    public string AggregateId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public int EventVersion { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public string? Metadata { get; private set; }
}
