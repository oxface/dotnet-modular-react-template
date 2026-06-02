using System.Reflection;

namespace Bondstone.Commands;

public static class ModuleCommandTypes
{
    public static Type[] FromHandlerAssemblyMarkers(params Type[] commandHandlerAssemblyMarkers)
    {
        ArgumentNullException.ThrowIfNull(commandHandlerAssemblyMarkers);

        if (commandHandlerAssemblyMarkers.Any(marker => marker is null))
        {
            throw new ArgumentException(
                "Command handler assembly markers must not contain null values.",
                nameof(commandHandlerAssemblyMarkers));
        }

        return commandHandlerAssemblyMarkers
            .Select(marker => marker.Assembly)
            .Distinct()
            .SelectMany(FindHandledCommandTypes)
            .Distinct()
            .ToArray();
    }

    internal static IEnumerable<Type> FindCommandHandlerTypes(IEnumerable<Type> assemblyMarkers)
    {
        return assemblyMarkers
            .Select(marker => marker.Assembly)
            .Distinct()
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => type.AsType())
            .Where(type => type.GetInterfaces().Any(IsCommandHandlerInterface));
    }

    internal static IEnumerable<Type> FindHandledCommandTypes(Assembly assembly)
    {
        return assembly.DefinedTypes
            .SelectMany(type => type.ImplementedInterfaces)
            .Where(IsCommandHandlerInterface)
            .Select(interfaceType => interfaceType.GetGenericArguments()[0]);
    }

    internal static bool IsCommandHandlerInterface(Type interfaceType)
    {
        if (!interfaceType.IsGenericType)
        {
            return false;
        }

        return interfaceType.GetGenericTypeDefinition() == typeof(IModuleCommandHandler<,>);
    }
}
