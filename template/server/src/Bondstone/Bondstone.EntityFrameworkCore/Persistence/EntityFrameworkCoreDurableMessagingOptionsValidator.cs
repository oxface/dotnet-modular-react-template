using Microsoft.Extensions.Options;
using Bondstone.Internal;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Persistence;

internal sealed class EntityFrameworkCoreDurableMessagingOptionsValidator(
    IEnumerable<ModulePersistenceRegistration> persistenceRegistrations,
    IEnumerable<ModuleMessageHandlerRegistration> messageHandlerRegistrations)
    : IValidateOptions<DurableMessagingOptions>
{
    public ValidateOptionsResult Validate(string? name, DurableMessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        string[] configuredModules;

        try
        {
            configuredModules = options.Modules.TrimDistinctRequired(nameof(options.Modules));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return ValidateOptionsResult.Success;
        }

        ValidateRegisteredModules(
            "module persistence registration",
            persistenceRegistrations.Select(registration => registration.ModuleName),
            configuredModules,
            failures);
        ValidateOnePersistenceContextPerModule(persistenceRegistrations, failures);
        ValidateMessageHandlersHavePersistence(
            persistenceRegistrations,
            messageHandlerRegistrations,
            failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRegisteredModules(
        string registrationKind,
        IEnumerable<string> moduleNames,
        IReadOnlyCollection<string> configuredModules,
        ICollection<string> failures)
    {
        foreach (string moduleName in moduleNames
            .Select(moduleName => moduleName.TrimRequired(nameof(moduleName)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            if (configuredModules.Contains(moduleName, StringComparer.Ordinal))
            {
                continue;
            }

            failures.Add(
                $"Messaging:Modules does not contain module '{moduleName}' used by {registrationKind}.");
        }
    }

    private static void ValidateOnePersistenceContextPerModule(
        IEnumerable<ModulePersistenceRegistration> persistenceRegistrations,
        ICollection<string> failures)
    {
        foreach (var moduleRegistrations in persistenceRegistrations
            .GroupBy(registration => registration.ModuleName.TrimRequired(nameof(registration.ModuleName)))
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Type[] dbContextTypes = moduleRegistrations
                .Select(registration => registration.DbContextType)
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            if (dbContextTypes.Length <= 1)
            {
                continue;
            }

            string contextNames = string.Join(
                ", ",
                dbContextTypes.Select(type => type.FullName));

            failures.Add(
                $"Module '{moduleRegistrations.Key}' has multiple module persistence DbContexts: {contextNames}.");
        }
    }

    private static void ValidateMessageHandlersHavePersistence(
        IEnumerable<ModulePersistenceRegistration> persistenceRegistrations,
        IEnumerable<ModuleMessageHandlerRegistration> messageHandlerRegistrations,
        ICollection<string> failures)
    {
        HashSet<string> persistentModules = persistenceRegistrations
            .Select(registration => registration.ModuleName.TrimRequired(nameof(registration.ModuleName)))
            .ToHashSet(StringComparer.Ordinal);

        foreach (string moduleName in messageHandlerRegistrations
            .Select(registration => registration.ModuleName.TrimRequired(nameof(registration.ModuleName)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
        {
            if (persistentModules.Contains(moduleName))
            {
                continue;
            }

            failures.Add(
                $"Module '{moduleName}' registers durable message handlers but does not register module persistence.");
        }
    }
}
