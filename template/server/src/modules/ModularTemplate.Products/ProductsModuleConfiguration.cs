using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Products.Contracts.Products;

namespace ModularTemplate.Products;

public static class ProductsModuleConfiguration
{
    public static IServiceCollection AddProductsApplicationServices(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/products/{productId:guid}",
                async Task<Results<Ok<ProductDetails>, NotFound>> (
                    [FromRoute] Guid productId,
                    [FromServices] IProductsQueries productsQueries,
                    CancellationToken cancellationToken) =>
                {
                    ProductDetails? product = await productsQueries.GetProductAsync(
                        productId,
                        cancellationToken);

                    if (product is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(product);
                })
            .RequireAuthorization()
            .WithName("GetProduct")
            .WithTags("Products")
            .Produces<ProductDetails>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
