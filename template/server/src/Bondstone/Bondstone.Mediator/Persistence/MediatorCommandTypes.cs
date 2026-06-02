using System.Reflection;
using Mediator;

namespace Bondstone.Mediator.Persistence;

public static class MediatorCommandTypes
{
    public static Type[] FromHandlerAssemblyMarkers(
        params Type[] commandHandlerAssemblyMarkers)
    {
        ArgumentNullException.ThrowIfNull(commandHandlerAssemblyMarkers);

        if (commandHandlerAssemblyMarkers.Any(marker => marker is null))
        {
            throw new ArgumentException(
                "Command handler assembly marker types must not contain null values.",
                nameof(commandHandlerAssemblyMarkers));
        }

        return commandHandlerAssemblyMarkers
            .Select(marker => marker.Assembly)
            .Distinct()
            .SelectMany(FindHandledCommandTypes)
            .Distinct()
            .ToArray();
    }

    private static IEnumerable<Type> FindHandledCommandTypes(Assembly assembly)
    {
        return assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.ImplementedInterfaces)
            .Where(IsCommandHandlerInterface)
            .Select(handlerInterface => handlerInterface.GenericTypeArguments[0]);
    }

    private static bool IsCommandHandlerInterface(Type interfaceType)
    {
        if (!interfaceType.IsGenericType)
        {
            return false;
        }

        Type genericDefinition = interfaceType.GetGenericTypeDefinition();
        return genericDefinition == typeof(ICommandHandler<>)
            || genericDefinition == typeof(ICommandHandler<,>);
    }
}
