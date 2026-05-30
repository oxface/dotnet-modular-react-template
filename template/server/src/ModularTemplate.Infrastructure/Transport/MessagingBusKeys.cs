using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Transport;

public static class MessagingBusKeys
{
    public static string ModuleQueue(string moduleName)
    {
        return $"{moduleName.TrimRequired(nameof(moduleName))}:queue";
    }
}
