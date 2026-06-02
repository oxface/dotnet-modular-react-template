using Microsoft.Extensions.DependencyInjection;

namespace Bondstone.Commands;

public sealed class ModuleCommandOptions
{
    public IList<Type> AssemblyMarkers { get; } = [];

    public IList<Type> PipelineBehaviors { get; } = [];

    public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;
}
