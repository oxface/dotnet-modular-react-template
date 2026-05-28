using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Transport;

internal static class MessageTypeRegistryFactory
{
    public static IMessageTypeRegistry Create(IEnumerable<MessagingRegistrationSource> sources)
    {
        var registry = new MessageTypeRegistry();

        foreach (MessagingRegistrationSource source in sources)
        {
            registry.RegisterFromAssembly(source.Assembly);
        }

        return registry;
    }
}
