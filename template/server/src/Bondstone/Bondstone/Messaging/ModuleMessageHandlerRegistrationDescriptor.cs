namespace Bondstone.Messaging;

internal sealed record ModuleMessageHandlerRegistrationDescriptor(
    Type HandlerType,
    Type MessageType,
    string MessageIdentity,
    string HandlerIdentity);
