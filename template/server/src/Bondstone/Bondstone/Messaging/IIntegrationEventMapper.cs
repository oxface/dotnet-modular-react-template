using Bondstone.Domain;

namespace Bondstone.Messaging;

/// <summary>
/// Non-generic base for resolving integration event mappers without reflection.
/// </summary>
public interface IIntegrationEventMapper
{
    string SourceModule { get; }

    IReadOnlyCollection<IIntegrationEvent> Map(IDomainEvent domainEvent);
}

/// <summary>
/// Maps a strongly typed domain event to zero or more integration events for the outbox.
/// Implement this per domain event that needs to cross module boundaries.
/// </summary>
public interface IIntegrationEventMapper<in TDomainEvent> : IIntegrationEventMapper
    where TDomainEvent : IDomainEvent
{
    IReadOnlyCollection<IIntegrationEvent> Map(TDomainEvent domainEvent);

    IReadOnlyCollection<IIntegrationEvent> IIntegrationEventMapper.Map(IDomainEvent domainEvent) =>
        Map((TDomainEvent)domainEvent);
}
