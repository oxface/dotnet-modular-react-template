namespace ModularTemplate.Infrastructure.Persistence;

public sealed class ModuleUnitOfWorkResolver(
    IEnumerable<ModulePersistenceRegistration> registrations,
    IModulePersistenceResolver persistenceResolver)
    : IModuleUnitOfWorkResolver
{
    public IModuleUnitOfWork? Resolve(Type commandType)
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

        string moduleName = matchingModuleNames[0];
        return persistenceResolver.ResolveUnitOfWork(moduleName);
    }
}
