using Microsoft.Extensions.Configuration;

namespace Bondstone.Transport.Rebus;

public sealed class RebusTransportBuilder
{
    internal RebusTransportOptions Options { get; } = new();

    internal RebusInternalTransport InternalTransport { get; private set; }

    public RebusTransportBuilder Configure(Action<RebusTransportOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Options);
        return this;
    }

    internal void UseInternalTransport(RebusInternalTransport internalTransport)
    {
        InternalTransport = internalTransport;
    }
}

internal enum RebusInternalTransport
{
    None = 0,
    Postgres = 1
}

public static class RebusPostgresTransportBuilderExtensions
{
    public static RebusTransportBuilder UsePostgresInternalTransport(
        this RebusTransportBuilder builder,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.Bind(builder.Options);
        builder.UseInternalTransport(RebusInternalTransport.Postgres);
        return builder;
    }
}
