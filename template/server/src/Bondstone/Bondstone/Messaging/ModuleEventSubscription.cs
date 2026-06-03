namespace Bondstone.Messaging;

public sealed record ModuleEventSubscription(string ModuleName, Type EventType);
