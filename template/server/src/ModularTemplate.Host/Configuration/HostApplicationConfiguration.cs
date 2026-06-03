using System.Reflection;
using ModularTemplate.Host.Features.Auth;
using ModularTemplate.ServiceDefaults;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Bondstone.Transport.Rebus;

namespace ModularTemplate.Host.Configuration;

public enum HostApplicationMode
{
    Runtime,
    OpenApiDocumentGeneration,
}

public static class HostApplicationConfiguration
{
    public static HostApplicationMode DetectHostApplicationMode()
    {
        return Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider"
            ? HostApplicationMode.OpenApiDocumentGeneration
            : HostApplicationMode.Runtime;
    }

    public static WebApplicationBuilder ConfigureHostApplicationMode(
        this WebApplicationBuilder builder,
        HostApplicationMode mode)
    {
        if (mode == HostApplicationMode.OpenApiDocumentGeneration)
        {
            builder.SetConfigurationValueIfMissing(
                "ConnectionStrings:modular-template-host",
                "Host=localhost;Port=5432;Database=modular_template_openapi;Username=postgres;Password=postgres");
        }

        return builder;
    }

    public static WebApplicationBuilder AddModularTemplateHost(
        this WebApplicationBuilder builder,
        HostApplicationMode mode = HostApplicationMode.Runtime)
    {
        builder.AddServiceDefaults();

        builder.AddHostAuthentication();
        builder.AddProblemDetails();
        builder.Services.AddOpenApi();
        builder.Services.AddModularTemplateModules();
        builder.Services.AddModularCommandHandling();

        if (mode == HostApplicationMode.Runtime)
        {
            builder.AddRebusTransport(transport =>
                transport.UsePostgresInternalTransport(builder.Configuration.GetSection("Messaging:Rebus")));
            builder.Services.AddModuleOutboxDispatchers();
        }

        return builder;
    }

    public static WebApplication UseModularTemplateHost(this WebApplication app)
    {
        app.UseProblemDetails();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapDefaultEndpoints();
        app.MapAuthEndpoints();
        app.MapModularTemplateModuleEndpoints();

        return app;
    }

    private static void SetConfigurationValueIfMissing(
        this WebApplicationBuilder builder,
        string key,
        string value)
    {
        if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
        {
            builder.Configuration[key] = value;
        }
    }
}
