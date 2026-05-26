namespace ModularTemplate.SharedKernel.Messaging;

public interface IMessageTypeRegistry
{
    MessageTypeRegistration Register<TMessage>(string messageTypeName)
        where TMessage : IMessage;

    MessageTypeRegistration Register(Type clrType, string messageTypeName);

    string GetMessageTypeName<TMessage>()
        where TMessage : IMessage;

    string GetMessageTypeName(Type clrType);

    Type ResolveClrType(string messageTypeName);

    bool TryResolveClrType(string messageTypeName, out Type? clrType);
}
