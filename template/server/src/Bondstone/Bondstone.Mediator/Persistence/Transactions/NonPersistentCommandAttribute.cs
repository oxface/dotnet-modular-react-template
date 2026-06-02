namespace Bondstone.Mediator.Persistence.Transactions;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NonPersistentCommandAttribute : Attribute;
