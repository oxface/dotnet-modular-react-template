using Bondstone;

namespace Bondstone.EntityFrameworkCore.Persistence;

public sealed class ModuleUnitOfWorkResolver(
    IEnumerable<ModulePersistenceRegistration> registrations,
    IModulePersistenceResolver persistenceResolver)
    : IModuleUnitOfWorkResolver, IModuleCommandBoundaryResolver
{
    public IModuleUnitOfWork? Resolve(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        string? moduleName = ResolveModuleName(commandType);

        return moduleName is null
            ? null
            : persistenceResolver.ResolveUnitOfWork(moduleName);
    }

    public string? ResolveModuleName(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        string[] matchingModuleNames = registrations
            .Where(registration => registration.HandlesCommand(commandType))
            .Select(registration => registration.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (matchingModuleNames.Length == 0)
        {
            return null;
        }

        if (matchingModuleNames.Length > 1)
        {
            string modules = string.Join(
                ", ",
                matchingModuleNames.Order());

            throw new InvalidOperationException(
                $"Command type '{commandType.FullName}' is mapped to more than one module persistence registration: {modules}.");
        }

        return matchingModuleNames[0];
    }
}
