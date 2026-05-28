namespace ModularTemplate.Infrastructure.Persistence;

public sealed class ModuleUnitOfWorkResolver(
    IEnumerable<ModulePersistenceRegistration> registrations,
    IEnumerable<IModuleUnitOfWork> unitOfWorks)
    : IModuleUnitOfWorkResolver
{
    public IModuleUnitOfWork? Resolve(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        ModulePersistenceRegistration[] matchingRegistrations = registrations
            .Where(registration => registration.HandlesCommand(commandType))
            .ToArray();

        if (matchingRegistrations.Length == 0)
        {
            return null;
        }

        if (matchingRegistrations.Length > 1)
        {
            string modules = string.Join(
                ", ",
                matchingRegistrations.Select(registration => registration.ModuleName).Order());

            throw new InvalidOperationException(
                $"Command type '{commandType.FullName}' is mapped to more than one module persistence registration: {modules}.");
        }

        string moduleName = matchingRegistrations[0].ModuleName;
        IModuleUnitOfWork[] matchingUnitOfWorks = unitOfWorks
            .Where(unitOfWork => string.Equals(unitOfWork.ModuleName, moduleName, StringComparison.Ordinal))
            .ToArray();

        return matchingUnitOfWorks.Length switch
        {
            1 => matchingUnitOfWorks[0],
            0 => throw new InvalidOperationException(
                $"No module unit of work is registered for module '{moduleName}'."),
            _ => throw new InvalidOperationException(
                $"Multiple module unit of work instances are registered for module '{moduleName}'."),
        };
    }
}
