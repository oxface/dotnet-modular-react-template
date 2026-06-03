using Microsoft.Extensions.Options;

namespace Bondstone.Transport.Rebus;

internal sealed class RebusTransportOptionsValidator : IValidateOptions<RebusTransportOptions>
{
    public ValidateOptionsResult Validate(string? name, RebusTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        RebusTransportOptionValidation.ValidateRequiredString(
            options.QueuePrefix,
            "Messaging:Rebus:QueuePrefix",
            failures);
        switch (options.InternalTransport)
        {
            case RebusInternalTransport.Postgres:
                RebusPostgresTransportOptionsValidator.Validate(options.Postgres, failures);
                break;
            case RebusInternalTransport.AzureServiceBus:
                RebusAzureServiceBusTransportOptionsValidator.Validate(options.AzureServiceBus, failures);
                break;
            case RebusInternalTransport.None:
                failures.Add("Rebus internal transport is not configured.");
                break;
            default:
                failures.Add($"Unsupported Rebus internal transport '{options.InternalTransport}'.");
                break;
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

internal static class RebusTransportOptionValidation
{
    public static void ValidateRequiredString(
        string? value,
        string optionName,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{optionName} is required.");
        }
    }

    public static void ValidateSqlIdentifier(
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
}

internal static class RebusPostgresTransportOptionsValidator
{
    public static void Validate(
        RebusPostgresTransportOptions options,
        ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(failures);

        RebusTransportOptionValidation.ValidateRequiredString(
            options.ConnectionStringName,
            "Messaging:Rebus:Postgres:ConnectionStringName",
            failures);
        RebusTransportOptionValidation.ValidateRequiredString(
            options.TransportSchema,
            "Messaging:Rebus:Postgres:TransportSchema",
            failures);
        RebusTransportOptionValidation.ValidateRequiredString(
            options.TransportTable,
            "Messaging:Rebus:Postgres:TransportTable",
            failures);
        RebusTransportOptionValidation.ValidateRequiredString(
            options.SubscriptionTable,
            "Messaging:Rebus:Postgres:SubscriptionTable",
            failures);
        RebusTransportOptionValidation.ValidateSqlIdentifier(
            options.TransportSchema,
            "Messaging:Rebus:Postgres:TransportSchema",
            failures);
        RebusTransportOptionValidation.ValidateSqlIdentifier(
            options.TransportTable,
            "Messaging:Rebus:Postgres:TransportTable",
            failures);
        RebusTransportOptionValidation.ValidateSqlIdentifier(
            options.SubscriptionTable,
            "Messaging:Rebus:Postgres:SubscriptionTable",
            failures);
    }
}

internal static class RebusAzureServiceBusTransportOptionsValidator
{
    public static void Validate(
        RebusAzureServiceBusTransportOptions options,
        ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(failures);

        RebusTransportOptionValidation.ValidateRequiredString(
            options.ConnectionStringName,
            "Messaging:Rebus:AzureServiceBus:ConnectionStringName",
            failures);
    }
}
