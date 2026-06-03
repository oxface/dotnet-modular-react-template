namespace Bondstone.Messaging;

public sealed record ModuleMessageHandlerRegistration
{
    public ModuleMessageHandlerRegistration(
        string moduleName,
        Type messageType,
        Type handlerType,
        string messageIdentity)
        : this(moduleName, messageType, handlerType, messageIdentity, messageIdentity)
    {
    }

    public ModuleMessageHandlerRegistration(
        string moduleName,
        Type messageType,
        Type handlerType,
        string messageIdentity,
        string handlerIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerIdentity);

        ModuleName = moduleName.Trim();
        MessageType = messageType;
        HandlerType = handlerType;
        MessageIdentity = messageIdentity.Trim();
        HandlerIdentity = handlerIdentity.Trim();
    }

    public string ModuleName { get; }

    public Type MessageType { get; }

    public Type HandlerType { get; }

    public string MessageIdentity { get; }

    public string HandlerIdentity { get; }
}
