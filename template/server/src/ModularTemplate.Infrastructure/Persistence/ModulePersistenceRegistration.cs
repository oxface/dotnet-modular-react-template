using Mediator;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Persistence;

public sealed class ModulePersistenceRegistration
{
    public ModulePersistenceRegistration(
        string moduleName,
        Type dbContextType,
        IReadOnlyCollection<Type> commandTypes)
    {
        ArgumentNullException.ThrowIfNull(dbContextType);
        ArgumentNullException.ThrowIfNull(commandTypes);

        Type[] normalizedCommandTypes = commandTypes
            .Select(commandType => commandType
                ?? throw new ArgumentException(
                    "Command types must not contain null values.",
                    nameof(commandTypes)))
            .Distinct()
            .ToArray();

        foreach (Type commandType in normalizedCommandTypes)
        {
            if (!typeof(IBaseCommand).IsAssignableFrom(commandType))
            {
                throw new ArgumentException(
                    $"Type '{commandType.FullName}' must implement {nameof(IBaseCommand)}.",
                    nameof(commandTypes));
            }
        }

        ModuleName = moduleName.TrimRequired(nameof(moduleName));
        DbContextType = dbContextType;
        CommandTypes = normalizedCommandTypes;
    }

    public string ModuleName { get; }

    public Type DbContextType { get; }

    public IReadOnlyCollection<Type> CommandTypes { get; }

    public bool HandlesCommand(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        return CommandTypes.Contains(commandType);
    }
}
