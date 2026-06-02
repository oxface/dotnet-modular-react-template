namespace Bondstone.Domain;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DomainEventTypeAttribute(
    string aggregateType,
    string name,
    int version) : Attribute
{
    public string AggregateType { get; } = aggregateType;

    public string Name { get; } = name;

    public int Version { get; } = version;
}
