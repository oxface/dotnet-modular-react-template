using Bondstone.Internal;

namespace Bondstone.Transport.Rebus;

public static class MessagingBusKeys
{
    public static string ModuleQueue(string moduleName)
    {
        return $"{moduleName.TrimRequired(nameof(moduleName))}:queue";
    }
}
