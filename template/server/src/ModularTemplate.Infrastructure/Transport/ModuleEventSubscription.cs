namespace ModularTemplate.Infrastructure.Transport;

internal sealed record ModuleEventSubscription(string ModuleName, Type EventType);
