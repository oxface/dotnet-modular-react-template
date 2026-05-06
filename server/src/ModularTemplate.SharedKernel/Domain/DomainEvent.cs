namespace ModularTemplate.SharedKernel.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }

    DateTimeOffset OccurredAt { get; }

    string ModuleName { get; }

    string Name { get; }

    int Version { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public abstract string ModuleName { get; }

    public abstract string Name { get; }

    public abstract int Version { get; }
}
