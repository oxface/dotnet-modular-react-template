using Microsoft.AspNetCore.Authentication;
using ModularTemplate.Host.Configuration;
using ModularTemplate.SharedKernel.Extensions;

namespace ModularTemplate.Host.Features.Auth;

public static class AuthEndpoint
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/auth/login",
                (string? returnUrl) =>
                {
                    string redirectUri = returnUrl.ToSafeLocalReturnUrl();

                    return TypedResults.Challenge(
                        new AuthenticationProperties { RedirectUri = redirectUri },
                        [HostAuthenticationConfiguration.OpenIdConnectScheme]);
                })
            .ExcludeFromDescription();

        endpoints.MapPost(
                "/auth/logout",
                () => TypedResults.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [
                        HostAuthenticationConfiguration.CookieScheme,
                        HostAuthenticationConfiguration.OpenIdConnectScheme,
                    ]))
            .ExcludeFromDescription();

        return endpoints;
    }
}
