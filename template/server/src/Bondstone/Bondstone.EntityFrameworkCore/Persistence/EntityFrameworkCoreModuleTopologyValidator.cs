using Microsoft.Extensions.Options;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Persistence;

internal sealed class EntityFrameworkCoreModuleTopologyValidator(
    IEnumerable<ModulePersistenceRegistration> persistenceRegistrations,
    IEnumerable<ModuleRuntimeRegistration> runtimeRegistrations)
    : IValidateOptions<DurableMessagingOptions>
{
    public ValidateOptionsResult Validate(string? name, DurableMessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        string[] moduleNames = persistenceRegistrations
            .Select(registration => registration.ModuleName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        ValidateOneRuntimePerModule(runtimeRegistrations, moduleNames, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateOneRuntimePerModule(
        IEnumerable<ModuleRuntimeRegistration> runtimeRegistrations,
        IReadOnlyCollection<string> persistenceModuleNames,
        ICollection<string> failures)
    {
        string[] normalizedRuntimeModuleNames = runtimeRegistrations
            .Select(registration => registration.ModuleName)
            .Where(moduleName => !string.IsNullOrWhiteSpace(moduleName))
            .Select(moduleName => moduleName.Trim())
            .ToArray();

        foreach (string moduleName in persistenceModuleNames)
        {
            int runtimeCount = normalizedRuntimeModuleNames.Count(runtimeModuleName =>
                string.Equals(runtimeModuleName, moduleName, StringComparison.Ordinal));

            if (runtimeCount == 1)
            {
                continue;
            }

            failures.Add(
                $"Module '{moduleName}' must have exactly one module runtime registration; found {runtimeCount}.");
        }

        foreach (string runtimeModuleName in normalizedRuntimeModuleNames
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            if (persistenceModuleNames.Contains(runtimeModuleName, StringComparer.Ordinal))
            {
                continue;
            }

            failures.Add(
                $"Module '{runtimeModuleName}' has a module runtime registration but no module persistence registration.");
        }
    }
}
