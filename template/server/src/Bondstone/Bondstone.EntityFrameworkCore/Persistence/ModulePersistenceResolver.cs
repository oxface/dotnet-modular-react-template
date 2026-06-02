using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Internal;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class ModulePersistenceResolver(
    IEnumerable<ModulePersistenceRegistration> registrations,
    IServiceProvider serviceProvider)
    : IModulePersistenceResolver
{
    public IModuleDbContext ResolveDbContext(string moduleName)
    {
        ModulePersistenceRegistration registration = ResolveRegistration(moduleName);
        IModuleDbContext dbContext = registration.DbContextFactory(serviceProvider);
        ValidateResolvedModuleName(registration, dbContext.ModuleName, "DbContext");
        return dbContext;
    }

    public IModuleUnitOfWork ResolveUnitOfWork(string moduleName)
    {
        ModulePersistenceRegistration registration = ResolveRegistration(moduleName);
        IModuleUnitOfWork unitOfWork = registration.UnitOfWorkFactory(serviceProvider);
        ValidateResolvedModuleName(registration, unitOfWork.ModuleName, "unit of work");
        return unitOfWork;
    }

    public IOutboxWriter ResolveOutboxWriter(string moduleName)
    {
        ModulePersistenceRegistration registration = ResolveRegistration(moduleName);
        IOutboxWriter outboxWriter = registration.OutboxWriterFactory(serviceProvider);
        ValidateResolvedModuleName(registration, outboxWriter.ModuleName, "outbox writer");
        return outboxWriter;
    }

    private ModulePersistenceRegistration ResolveRegistration(string moduleName)
    {
        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        ModulePersistenceRegistration[] matchingRegistrations = registrations
            .Where(registration => string.Equals(registration.ModuleName, normalizedModuleName, StringComparison.Ordinal))
            .ToArray();

        if (matchingRegistrations.Length == 0)
        {
            throw new InvalidOperationException(
                $"No module persistence registration exists for module '{normalizedModuleName}'.");
        }

        Type[] dbContextTypes = matchingRegistrations
            .Select(registration => registration.DbContextType)
            .Distinct()
            .ToArray();

        if (dbContextTypes.Length > 1)
        {
            string contextNames = string.Join(
                ", ",
                dbContextTypes
                    .OrderBy(type => type.FullName, StringComparer.Ordinal)
                    .Select(type => type.FullName));

            throw new InvalidOperationException(
                $"Module '{normalizedModuleName}' has multiple module persistence DbContexts: {contextNames}.");
        }

        return matchingRegistrations[0];
    }

    private static void ValidateResolvedModuleName(
        ModulePersistenceRegistration registration,
        string resolvedModuleName,
        string componentName)
    {
        string normalizedResolvedModuleName = resolvedModuleName.TrimRequired(nameof(resolvedModuleName));
        if (string.Equals(registration.ModuleName, normalizedResolvedModuleName, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Module persistence registration for module '{registration.ModuleName}' resolves a {componentName} " +
            $"for module '{normalizedResolvedModuleName}' from DbContext '{registration.DbContextType.FullName}'.");
    }
}
