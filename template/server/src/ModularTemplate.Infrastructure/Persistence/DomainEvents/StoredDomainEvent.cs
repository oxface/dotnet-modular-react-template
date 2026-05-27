using System.Text.Json;
using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Infrastructure.Persistence.DomainEvents;

public sealed class StoredDomainEvent
{
    private StoredDomainEvent(
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

    public static StoredDomainEvent FromDomainEvent(IDomainEvent domainEvent, string aggregateId)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        DomainEventTypeAttribute eventType = domainEvent.GetType()
            .GetCustomAttributes(typeof(DomainEventTypeAttribute), inherit: false)
            .OfType<DomainEventTypeAttribute>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Domain event '{domainEvent.GetType().FullName}' must declare {nameof(DomainEventTypeAttribute)}.");

        return new StoredDomainEvent(
            domainEvent.EventId, domainEvent.OccurredAt,
            eventType.AggregateType, aggregateId,
            eventType.Name, eventType.Version,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            JsonSerializer.Serialize(new { ClrType = domainEvent.GetType().FullName }));
    }
}
