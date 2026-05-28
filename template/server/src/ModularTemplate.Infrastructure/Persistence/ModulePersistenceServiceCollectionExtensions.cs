using System.Reflection;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Persistence;

public static class ModulePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddModulePersistence<TDbContext>(
        this IServiceCollection services,
        string moduleName,
        params Type[] commandHandlerAssemblyMarkers)
        where TDbContext : DbContext, IModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandHandlerAssemblyMarkers);

        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        if (commandHandlerAssemblyMarkers.Any(marker => marker is null))
        {
            throw new ArgumentException(
                "Command handler assembly marker types must not contain null values.",
                nameof(commandHandlerAssemblyMarkers));
        }

        Type[] commandTypes = FindHandledCommandTypes(commandHandlerAssemblyMarkers);
        services.TryAddScoped<IModuleUnitOfWorkContext, ModuleUnitOfWorkContext>();
        services.AddScoped<IModuleUnitOfWork, ModuleUnitOfWork<TDbContext>>();
        services.AddSingleton(new ModulePersistenceRegistration(
            normalizedModuleName,
            typeof(TDbContext),
            commandTypes
                .Distinct()
                .ToArray()));

        return services;
    }

    private static Type[] FindHandledCommandTypes(IEnumerable<Type> commandHandlerAssemblyMarkers)
    {
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
