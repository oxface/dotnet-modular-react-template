using System.Reflection;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Infrastructure.Persistence;

public sealed class ModulePersistenceRegistration
{
    public ModulePersistenceRegistration(
        string moduleName,
        Type dbContextType,
        IReadOnlyCollection<Assembly> commandAssemblies)
    {
        ArgumentNullException.ThrowIfNull(dbContextType);
        ArgumentNullException.ThrowIfNull(commandAssemblies);

        Assembly[] normalizedCommandAssemblies = commandAssemblies
            .Select(assembly => assembly
                ?? throw new ArgumentException(
                    "Command assemblies must not contain null values.",
                    nameof(commandAssemblies)))
            .Distinct()
            .ToArray();

        if (normalizedCommandAssemblies.Length == 0)
        {
            throw new ArgumentException(
                "At least one command assembly is required.",
                nameof(commandAssemblies));
        }

        ModuleName = moduleName.TrimRequired(nameof(moduleName));
        DbContextType = dbContextType;
        CommandAssemblies = normalizedCommandAssemblies;
    }

    public string ModuleName { get; }

    public Type DbContextType { get; }

    public IReadOnlyCollection<Assembly> CommandAssemblies { get; }

    public bool HandlesCommand(Type commandType)
    {
        ArgumentNullException.ThrowIfNull(commandType);

        return CommandAssemblies.Contains(commandType.Assembly);
    }
}
