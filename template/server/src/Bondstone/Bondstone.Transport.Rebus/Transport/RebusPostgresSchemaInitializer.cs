using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bondstone.Transport.Rebus;

public sealed class RebusPostgresSchemaInitializer(
    IConfiguration configuration,
    IOptions<RebusTransportOptions> options)
{
    private readonly RebusTransportOptions _options = options.Value;

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        string connectionString = configuration.GetConnectionString(_options.Postgres.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{_options.Postgres.ConnectionStringName}' is required for Rebus PostgreSQL transport.");

        string schema = DelimitSchema(_options.Postgres.TransportSchema);
        string subscriptionTable = DelimitIdentifier(_options.Postgres.SubscriptionTable);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText =
            $"""
            CREATE SCHEMA IF NOT EXISTS {schema};
            CREATE TABLE IF NOT EXISTS {schema}.{subscriptionTable} (
                "topic" VARCHAR(200) NOT NULL,
                "address" VARCHAR(200) NOT NULL,
                PRIMARY KEY ("topic", "address")
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string DelimitSchema(string schema)
    {
        return DelimitIdentifier(schema, "transport schema");
    }

    private static string DelimitIdentifier(string value)
    {
        return DelimitIdentifier(value, "transport identifier");
    }

    private static string DelimitIdentifier(string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Any(static c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new InvalidOperationException($"Invalid {description} '{value}'.");
        }

        return "\"" + value + "\"";
    }
}
