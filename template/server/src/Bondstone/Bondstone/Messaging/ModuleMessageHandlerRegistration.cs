namespace Bondstone.Messaging;

public sealed record ModuleMessageHandlerRegistration(
    string ModuleName,
    Type MessageType,
    Type HandlerType,
    string MessageIdentity);
