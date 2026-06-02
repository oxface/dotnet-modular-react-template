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
        if (!_options.Postgres.AutoCreateSchema)
        {
            return;
        }

        string connectionString = configuration.GetConnectionString(_options.Postgres.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{_options.Postgres.ConnectionStringName}' is required for Rebus PostgreSQL transport.");

        string schema = DelimitSchema(_options.Postgres.TransportSchema);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA IF NOT EXISTS {schema}";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string DelimitSchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema)
            || schema.Any(static c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
        {
            throw new InvalidOperationException($"Invalid transport schema name '{schema}'.");
        }

        return "\"" + schema + "\"";
    }
}
