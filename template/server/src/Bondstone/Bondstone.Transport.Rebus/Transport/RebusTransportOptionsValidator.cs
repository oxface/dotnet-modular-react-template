using Microsoft.Extensions.Options;

namespace Bondstone.Transport.Rebus;

internal sealed class RebusTransportOptionsValidator : IValidateOptions<RebusTransportOptions>
{
    public ValidateOptionsResult Validate(string? name, RebusTransportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        ValidateRequiredString(options.QueuePrefix, "Messaging:Rebus:QueuePrefix", failures);
        ValidateRequiredString(options.Postgres.ConnectionStringName, "Messaging:Rebus:Postgres:ConnectionStringName", failures);
        ValidateRequiredString(options.Postgres.TransportSchema, "Messaging:Rebus:Postgres:TransportSchema", failures);
        ValidateRequiredString(options.Postgres.TransportTable, "Messaging:Rebus:Postgres:TransportTable", failures);
        ValidateRequiredString(options.Postgres.SubscriptionTable, "Messaging:Rebus:Postgres:SubscriptionTable", failures);
        ValidateSqlIdentifier(options.Postgres.TransportSchema, "Messaging:Rebus:Postgres:TransportSchema", failures);
        ValidateSqlIdentifier(options.Postgres.TransportTable, "Messaging:Rebus:Postgres:TransportTable", failures);
        ValidateSqlIdentifier(options.Postgres.SubscriptionTable, "Messaging:Rebus:Postgres:SubscriptionTable", failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRequiredString(
        string? value,
        string optionName,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{optionName} is required.");
        }
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
}
