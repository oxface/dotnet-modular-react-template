using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ModularTemplate.Identity.Contracts.CurrentUser;

namespace ModularTemplate.Host.Features.CurrentUser;

public static class GetMeEndpoint
{
    public static IEndpointRouteBuilder MapCurrentUserEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/me",
                async Task<Results<Ok<GetMeResponse>, UnauthorizedHttpResult>> (
                    HttpContext httpContext,
                    [FromServices] ICurrentUserProvider currentUserProvider,
                    CancellationToken cancellationToken) =>
                {
                    AuthenticatedIdentity? identity =
                        AuthenticatedIdentityAdapter.FromClaimsPrincipal(httpContext.User);
                    CurrentUserContext currentUser = await currentUserProvider.GetCurrentUserAsync(
                        identity,
                        cancellationToken);

                    if (!currentUser.IsAuthenticated || currentUser.LocalUserId is null)
                    {
                        return TypedResults.Unauthorized();
                    }

                    return TypedResults.Ok(GetMeResponse.FromCurrentUser(currentUser));
                })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithTags("CurrentUser")
            .Produces<GetMeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}

public sealed record GetMeResponse(
    bool IsAuthenticated,
    GetMeUserResponse User,
    GetMeApplicationAccessResponse ApplicationAccess)
{
    public static GetMeResponse FromCurrentUser(CurrentUserContext currentUser)
    {
        return new GetMeResponse(
            true,
            new GetMeUserResponse(
                currentUser.LocalUserId?.ToString()
                    ?? throw new InvalidOperationException("Current user must have a local user id."),
                currentUser.DisplayName,
                currentUser.Email),
            new GetMeApplicationAccessResponse(currentUser.HasApplicationAccess));
    }
}

public sealed record GetMeUserResponse(
    string Id,
    string? DisplayName,
    string? Email);

public sealed record GetMeApplicationAccessResponse(bool HasAccess);
