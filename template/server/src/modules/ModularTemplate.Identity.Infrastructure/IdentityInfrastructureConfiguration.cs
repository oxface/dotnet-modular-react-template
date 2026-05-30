using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Identity.Contracts.Authorization;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Users;
using ModularTemplate.Identity.Access;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Transport;

namespace ModularTemplate.Identity.Infrastructure;

public static class IdentityInfrastructureConfiguration
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddIdentityApplicationServices();
        services.AddIdentityInfrastructure();

        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            string connectionString = sp.GetRequiredService<IConfiguration>()
                .GetConnectionString("modular-template-host")
                ?? throw new InvalidOperationException(
                    "Connection string 'modular-template-host' is required.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity"));
        });

        services.AddScoped<ILocalUserRepository, LocalUserRepository>();
        services.AddScoped<IApplicationAccessRepository, ApplicationAccessRepository>();
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IOutboxWriter, OutboxWriter<IdentityDbContext>>();
        services.AddModulePersistence<IdentityDbContext>(
            "identity",
            typeof(GrantInitialAdminAccessCommandHandler));
        services.AddModuleMessaging(
            "identity",
            typeof(GrantInitialAdminAccessCommand),
            typeof(IApplicationAccessAuthorizer),
            typeof(IdentityDbContext));

        return services;
    }
}
