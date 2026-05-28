using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Persistence;

public static class ModulePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddModulePersistence<TDbContext>(
        this IServiceCollection services,
        string moduleName,
        params Type[] commandAssemblyMarkers)
        where TDbContext : DbContext, IModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandAssemblyMarkers);

        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        if (commandAssemblyMarkers.Length == 0)
        {
            throw new ArgumentException(
                "At least one command assembly marker type is required.",
                nameof(commandAssemblyMarkers));
        }

        if (commandAssemblyMarkers.Any(marker => marker is null))
        {
            throw new ArgumentException(
                "Command assembly marker types must not contain null values.",
                nameof(commandAssemblyMarkers));
        }

        services.AddScoped<IModuleUnitOfWork, ModuleUnitOfWork<TDbContext>>();
        services.AddSingleton(new ModulePersistenceRegistration(
            normalizedModuleName,
            typeof(TDbContext),
            commandAssemblyMarkers
                .Select(marker => marker.Assembly)
                .Distinct()
                .ToArray()));

        return services;
    }
}
