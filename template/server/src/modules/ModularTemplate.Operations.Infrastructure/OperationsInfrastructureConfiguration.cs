using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Operations;
using ModularTemplate.Operations.Contracts.Operations;
using ModularTemplate.Operations.Operations;
using ModularTemplate.Operations.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Transport;

namespace ModularTemplate.Operations.Infrastructure;

public static class OperationsInfrastructureConfiguration
{
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

        services.AddScoped<IOperationsDbContext>(sp => sp.GetRequiredService<OperationsDbContext>());
        services.AddScoped<IOperationRepository, OperationRepository>();
        services.AddScoped<IOperationsQueries, OperationsQueries>();
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<OperationsDbContext>());
        services.AddScoped<IOutboxWriter, OutboxWriter<OperationsDbContext>>();
        services.AddModulePersistence<OperationsDbContext>(
            "operations",
            typeof(Operation),
            typeof(IOperationsQueries));
        services.AddMessagingAssembly<Operation>();
        services.AddMessagingAssembly<IOperationsQueries>();
        services.AddMessagingAssembly<OperationsDbContext>();

        return services;
    }
}
