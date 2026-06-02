namespace Bondstone.Domain;

public interface IAggregateRoot
{
    object Id { get; }

    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    IReadOnlyCollection<IDomainEvent> DequeueDomainEvents();
}
