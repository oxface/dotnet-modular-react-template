using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Outbox;
using Npgsql;

namespace ModularTemplate.Infrastructure.Transport;

internal sealed class TransportSchemaInitializerHostedService(
    IConfiguration configuration,
    IOptions<DurableMessagingOptions> options)
    : IHostedService
{
    private readonly DurableMessagingOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || _options.Transport != DurableMessagingTransport.Postgres)
        {
            return;
        }

        string connectionString = configuration.GetConnectionString(_options.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{_options.ConnectionStringName}' is required when Messaging:Transport is Postgres.");

        string schema = DelimitSchema(_options.TransportSchema);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE SCHEMA IF NOT EXISTS {schema}";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
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
