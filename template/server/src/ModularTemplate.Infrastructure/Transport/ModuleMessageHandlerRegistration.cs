namespace ModularTemplate.Infrastructure.Transport;

public sealed record ModuleMessageHandlerRegistration(
    string ModuleName,
    Type MessageType,
    Type HandlerType,
    string MessageIdentity);
