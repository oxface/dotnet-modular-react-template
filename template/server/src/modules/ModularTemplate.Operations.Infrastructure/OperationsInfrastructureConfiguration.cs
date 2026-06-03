using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Operations;
using ModularTemplate.Operations.Contracts.Operations;
using ModularTemplate.Operations.Operations;
using ModularTemplate.Operations.Infrastructure.Persistence;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Bondstone.Messaging;

namespace ModularTemplate.Operations.Infrastructure;

public static class OperationsInfrastructureConfiguration
{
    public static IServiceCollection AddOperationsModule(this IServiceCollection services)
    {
        services.AddOperationsApplicationServices();
        services.AddOperationsInfrastructure();

        return services;
    }

    public static IEndpointRouteBuilder MapOperationsModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOperationsEndpoints();

        return endpoints;
    }

    public static IServiceCollection AddOperationsInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<OperationsDbContext>((sp, options) =>
        {
            string connectionString = sp.GetRequiredService<IConfiguration>()
                .GetConnectionString("modular-template-host")
                ?? throw new InvalidOperationException(
                    "Connection string 'modular-template-host' is required.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "operations"));
        });

        services.AddScoped<IOperationRepository, OperationRepository>();
        services.AddScoped<IOperationsQueries, OperationsQueries>();
        services.AddModulePersistence<OperationsDbContext>("operations");
        services.AddModuleMessaging(
            "operations",
            typeof(Operation),
            typeof(IOperationsQueries),
            typeof(OperationsDbContext));

        return services;
    }
}
