using System.Collections.ObjectModel;
using System.Reflection;

namespace ModularTemplate.SharedKernel.Messaging;

public sealed class MessageTypeRegistry : IMessageTypeRegistry
{
    private readonly Dictionary<Type, string> _typeNamesByClrType = [];
    private readonly Dictionary<string, Type> _clrTypesByTypeName = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public MessageTypeRegistration Register<TMessage>()
        where TMessage : IMessage
    {
        return Register(typeof(TMessage));
    }

    public MessageTypeRegistration Register(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        MessageIdentityAttribute identity = clrType
            .GetCustomAttributes(typeof(MessageIdentityAttribute), inherit: false)
            .OfType<MessageIdentityAttribute>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Message type '{clrType.FullName}' must declare {nameof(MessageIdentityAttribute)}.");

        return Register(clrType, identity.Name);
    }

    public MessageTypeRegistration Register<TMessage>(string messageTypeName)
        where TMessage : IMessage
    {
        return Register(typeof(TMessage), messageTypeName);
    }

    public MessageTypeRegistration Register(Type clrType, string messageTypeName)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        if (!typeof(IMessage).IsAssignableFrom(clrType))
        {
            throw new ArgumentException(
                $"Type '{clrType.FullName}' must implement {nameof(IMessage)}.",
                nameof(clrType));
        }

        if (string.IsNullOrWhiteSpace(messageTypeName))
        {
            throw new ArgumentException("Message type name is required.", nameof(messageTypeName));
        }

        string normalizedTypeName = messageTypeName.Trim();

        lock (_sync)
        {
            if (_typeNamesByClrType.TryGetValue(clrType, out string? existingTypeName))
            {
                if (!string.Equals(existingTypeName, normalizedTypeName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Type '{clrType.FullName}' is already registered as '{existingTypeName}'.");
                }

                return new MessageTypeRegistration(clrType, existingTypeName);
            }

            if (_clrTypesByTypeName.TryGetValue(normalizedTypeName, out Type? existingClrType))
            {
                throw new InvalidOperationException(
                    $"Message type '{normalizedTypeName}' is already registered for '{existingClrType.FullName}'.");
            }

            _typeNamesByClrType.Add(clrType, normalizedTypeName);
            _clrTypesByTypeName.Add(normalizedTypeName, clrType);
            return new MessageTypeRegistration(clrType, normalizedTypeName);
        }
    }

    public IReadOnlyCollection<MessageTypeRegistration> RegisterFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(static type => typeof(IMessage).IsAssignableFrom(type)
                && type is { IsAbstract: false, IsInterface: false }
                && type.GetCustomAttributes(typeof(MessageIdentityAttribute), inherit: false).Any())
            .Select(Register)
            .ToArray();
    }

    public string GetMessageTypeName<TMessage>()
        where TMessage : IMessage
    {
        return GetMessageTypeName(typeof(TMessage));
    }

    public string GetMessageTypeName(Type clrType)
    {
        ArgumentNullException.ThrowIfNull(clrType);

        lock (_sync)
        {
            if (_typeNamesByClrType.TryGetValue(clrType, out string? messageTypeName))
            {
                return messageTypeName;
            }
        }

        throw new KeyNotFoundException($"No message type registration exists for '{clrType.FullName}'.");
    }

    public Type ResolveClrType(string messageTypeName)
    {
        if (TryResolveClrType(messageTypeName, out Type? clrType))
        {
            return clrType!;
        }

        throw new KeyNotFoundException($"No message CLR type registration exists for '{messageTypeName}'.");
    }

    public bool TryResolveClrType(string messageTypeName, out Type? clrType)
    {
        if (string.IsNullOrWhiteSpace(messageTypeName))
        {
            clrType = null;
            return false;
        }

        string normalizedTypeName = messageTypeName.Trim();

        lock (_sync)
        {
            return _clrTypesByTypeName.TryGetValue(normalizedTypeName, out clrType);
        }
    }

    public IReadOnlyDictionary<Type, string> RegisteredMessageTypesByClrType
    {
        get
        {
            lock (_sync)
            {
                return new ReadOnlyDictionary<Type, string>(new Dictionary<Type, string>(_typeNamesByClrType));
            }
        }
    }
}
