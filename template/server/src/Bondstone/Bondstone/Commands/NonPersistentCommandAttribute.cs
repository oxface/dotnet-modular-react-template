namespace Bondstone.Commands;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class NonPersistentCommandAttribute : Attribute;
