using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Operations.Contracts.Operations;

namespace ModularTemplate.Operations;

public static class OperationsModuleConfiguration
{
    public static IServiceCollection AddOperationsApplicationServices(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/operations/{operationId:guid}",
                async Task<Results<Ok<OperationDetails>, NotFound>> (
                    [FromRoute] Guid operationId,
                    [FromServices] IOperationsQueries operationsQueries,
                    CancellationToken cancellationToken) =>
                {
                    OperationDetails? operation = await operationsQueries.GetOperationAsync(
                        operationId,
                        cancellationToken);

                    if (operation is null)
                    {
                        return TypedResults.NotFound();
                    }

                    return TypedResults.Ok(operation);
                })
            .RequireAuthorization()
            .WithName("GetOperation")
            .WithTags("Operations")
            .Produces<OperationDetails>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
