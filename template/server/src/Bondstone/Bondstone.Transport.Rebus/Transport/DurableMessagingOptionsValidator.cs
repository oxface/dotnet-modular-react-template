using Microsoft.Extensions.Options;
using Bondstone.Internal;
using Bondstone.Messaging;

namespace Bondstone.Transport.Rebus;

internal sealed class DurableMessagingOptionsValidator(
    IEnumerable<ModuleMessageHandlerRegistration> messageHandlerRegistrations,
    IEnumerable<ModuleEventSubscription> eventSubscriptions)
    : IValidateOptions<DurableMessagingOptions>
{
    public ValidateOptionsResult Validate(string? name, DurableMessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidatePositive(options.PollingInterval, "Messaging:PollingInterval", failures);
        ValidatePositive(options.BatchSize, "Messaging:BatchSize", failures);
        ValidatePositive(options.MaxAttempts, "Messaging:MaxAttempts", failures);
        ValidatePositive(options.LockTimeout, "Messaging:LockTimeout", failures);
        ValidateRetryDelays(options.RetryDelays, failures);

        string[] configuredModules;
        try
        {
            configuredModules = options.Modules.TrimDistinctRequired(nameof(options.Modules));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            failures.Add("Messaging:Modules must contain at least one module name.");
            configuredModules = [];
        }

        foreach (string moduleName in configuredModules)
        {
            ValidateSqlIdentifier(moduleName, "Messaging:Modules", failures);
        }

        ValidateRegisteredModules(
            "module message handler registration",
            messageHandlerRegistrations.Select(registration => registration.ModuleName),
            configuredModules,
            failures);
        ValidateRegisteredModules(
            "module event subscription",
            eventSubscriptions.Select(subscription => subscription.ModuleName),
            configuredModules,
            failures);
        ValidateEventSubscriptionsHaveHandlers(
            messageHandlerRegistrations,
            eventSubscriptions,
            failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateSqlIdentifier(
        string? value,
        string optionName,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (value.Any(static c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            failures.Add($"{optionName} must contain only ASCII letters, digits, or underscores.");
        }
    }

    private static void ValidatePositive(
        TimeSpan value,
        string optionName,
        ICollection<string> failures)
    {
        if (value <= TimeSpan.Zero)
        {
            failures.Add($"{optionName} must be greater than zero.");
        }
    }

    private static void ValidatePositive(
        int value,
        string optionName,
        ICollection<string> failures)
    {
        if (value <= 0)
        {
            failures.Add($"{optionName} must be greater than zero.");
        }
    }

    private static void ValidateRetryDelays(
        IReadOnlyCollection<TimeSpan>? retryDelays,
        ICollection<string> failures)
    {
        if (retryDelays is null)
        {
            failures.Add("Messaging:RetryDelays is required.");
            return;
        }

        if (retryDelays.Any(delay => delay < TimeSpan.Zero))
        {
            failures.Add("Messaging:RetryDelays must not contain negative durations.");
        }
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

    private static void ValidateEventSubscriptionsHaveHandlers(
        IEnumerable<ModuleMessageHandlerRegistration> messageHandlerRegistrations,
        IEnumerable<ModuleEventSubscription> eventSubscriptions,
        ICollection<string> failures)
    {
        HashSet<(string ModuleName, Type MessageType)> handledMessages = messageHandlerRegistrations
            .Select(registration => (
                ModuleName: registration.ModuleName.TrimRequired(nameof(registration.ModuleName)),
                registration.MessageType))
            .ToHashSet();

        foreach ((string ModuleName, Type EventType) subscription in eventSubscriptions
            .Select(subscription => (
                ModuleName: subscription.ModuleName.TrimRequired(nameof(subscription.ModuleName)),
                subscription.EventType))
            .Distinct()
            .OrderBy(subscription => subscription.ModuleName, StringComparer.Ordinal)
            .ThenBy(subscription => subscription.EventType.FullName, StringComparer.Ordinal))
        {
            if (handledMessages.Contains((subscription.ModuleName, subscription.EventType)))
            {
                continue;
            }

            failures.Add(
                $"Module '{subscription.ModuleName}' subscribes to event '{subscription.EventType.FullName}' " +
                "but does not register a matching module message handler.");
        }
    }

}
