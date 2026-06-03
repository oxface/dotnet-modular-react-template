using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Bondstone.Internal;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class EntityFrameworkCoreModuleMigrator(
    IEnumerable<ModulePersistenceRegistration> modulePersistenceRegistrations,
    IServiceProvider serviceProvider,
    ILogger<EntityFrameworkCoreModuleMigrator> logger)
    : IEntityFrameworkCoreModuleMigrator
{
    public async Task MigrateAsync(string? moduleName = null, CancellationToken cancellationToken = default)
    {
        string? normalizedModuleName = moduleName?.TrimRequired(nameof(moduleName));
        ModulePersistenceRegistration[] registrations = modulePersistenceRegistrations
            .Where(registration => normalizedModuleName is null
                || string.Equals(registration.ModuleName, normalizedModuleName, StringComparison.Ordinal))
            .OrderBy(registration => registration.ModuleName, StringComparer.Ordinal)
            .ToArray();

        if (normalizedModuleName is not null && registrations.Length == 0)
        {
            throw new InvalidOperationException(
                $"No Entity Framework Core module persistence registration exists for module '{normalizedModuleName}'.");
        }

        foreach (ModulePersistenceRegistration registration in registrations)
        {
            IModuleDbContext moduleContext = registration.DbContextFactory(serviceProvider);
            if (moduleContext is not DbContext dbContext)
            {
                throw new InvalidOperationException(
                    $"Module '{registration.ModuleName}' DbContext registration resolved '{moduleContext.GetType().FullName}', " +
                    $"which does not inherit {nameof(DbContext)}.");
            }

            logger.LogInformation(
                "Migrating module {ModuleName} with DbContext {DbContextType}.",
                registration.ModuleName,
                registration.DbContextType.FullName);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
