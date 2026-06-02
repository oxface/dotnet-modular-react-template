using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bondstone.Commands;

public static class ModuleCommandServiceCollectionExtensions
{
    public static IServiceCollection AddModuleCommands(
        this IServiceCollection services,
        Action<ModuleCommandOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new ModuleCommandOptions();
        configure?.Invoke(options);

        services.TryAddScoped<IModuleCommandBus, ModuleCommandBus>();

        foreach (Type behaviorType in options.PipelineBehaviors)
        {
            services.TryAddEnumerable(ServiceDescriptor.Describe(
                typeof(IModuleCommandPipelineBehavior<,>),
                behaviorType,
                options.ServiceLifetime));
        }

        foreach (Type handlerType in ModuleCommandTypes.FindCommandHandlerTypes(options.AssemblyMarkers))
        {
            foreach (Type handlerInterface in handlerType.GetInterfaces()
                .Where(ModuleCommandTypes.IsCommandHandlerInterface))
            {
                services.TryAddEnumerable(
                    new ServiceDescriptor(handlerInterface, handlerType, options.ServiceLifetime));
            }
        }

        return services;
    }
}
