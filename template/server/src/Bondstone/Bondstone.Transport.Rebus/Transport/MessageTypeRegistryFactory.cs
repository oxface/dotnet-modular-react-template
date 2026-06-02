using Bondstone.Messaging;

namespace Bondstone.Transport.Rebus;

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
