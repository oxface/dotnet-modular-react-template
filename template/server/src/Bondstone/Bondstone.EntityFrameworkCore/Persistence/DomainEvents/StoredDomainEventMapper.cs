using System.Text.Json;
using Bondstone.Domain;

namespace Bondstone.EntityFrameworkCore.Persistence.DomainEvents;

public sealed class StoredDomainEventMapper : IStoredDomainEventMapper
{
    public StoredDomainEvent Map(IDomainEvent domainEvent, string aggregateId)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        DomainEventTypeAttribute eventType = domainEvent.GetType()
            .GetCustomAttributes(typeof(DomainEventTypeAttribute), inherit: false)
            .OfType<DomainEventTypeAttribute>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Domain event '{domainEvent.GetType().FullName}' must declare {nameof(DomainEventTypeAttribute)}.");

        return new StoredDomainEvent(
            domainEvent.EventId,
            domainEvent.OccurredAt,
            eventType.AggregateType,
            aggregateId,
            eventType.Name,
            eventType.Version,
            JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            JsonSerializer.Serialize(new { ClrType = domainEvent.GetType().FullName }));
    }
}
