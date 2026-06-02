namespace Bondstone.Transport.Rebus;

internal sealed record ModuleEventSubscription(string ModuleName, Type EventType);
