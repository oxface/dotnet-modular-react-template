using Bondstone.Internal;

namespace Bondstone.Messaging;

public sealed record ModuleTopologyRegistration
{
    public ModuleTopologyRegistration(string moduleName)
    {
        ModuleName = moduleName.TrimRequired(nameof(moduleName));
    }

    public string ModuleName { get; }
}
