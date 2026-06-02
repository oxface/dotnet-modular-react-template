namespace Bondstone.Messaging;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MessageIdentityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
