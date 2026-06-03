using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Products;
using ModularTemplate.Products.Contracts.Products;
using ModularTemplate.Products.Products;
using ModularTemplate.Products.Infrastructure.Persistence;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Bondstone.Messaging;

namespace ModularTemplate.Products.Infrastructure;

public static class ProductsInfrastructureConfiguration
{
    public static IServiceCollection AddProductsModule(this IServiceCollection services)
    {
        services.AddProductsApplicationServices();
        services.AddProductsInfrastructure();

        return services;
    }

    public static IEndpointRouteBuilder MapProductsModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProductsEndpoints();

        return endpoints;
    }

    public static IServiceCollection AddProductsInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<ProductsDbContext>((sp, options) =>
        {
            string connectionString = sp.GetRequiredService<IConfiguration>()
                .GetConnectionString("modular-template-host")
                ?? throw new InvalidOperationException(
                    "Connection string 'modular-template-host' is required.");

            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "products"));
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductsQueries, ProductsQueries>();
        services.AddModulePersistence<ProductsDbContext>("products");
        services.AddModuleMessaging(
            "products",
            typeof(Product),
            typeof(IProductsQueries),
            typeof(ProductsDbContext));

        return services;
    }
}
