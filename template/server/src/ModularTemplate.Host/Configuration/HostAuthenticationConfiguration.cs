using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ModularTemplate.Host.Authentication;

namespace ModularTemplate.Host.Configuration;

public static class HostAuthenticationConfiguration
{
    public const string CookieScheme = "ModularTemplate.Session";
    public const string OpenIdConnectScheme = OpenIdConnectDefaults.AuthenticationScheme;

    public static WebApplicationBuilder AddHostAuthentication(this WebApplicationBuilder builder)
    {
        HostAuthenticationOptions authOptions =
            builder.Configuration.GetSection(HostAuthenticationOptions.SectionName)
                .Get<HostAuthenticationOptions>()
            ?? new HostAuthenticationOptions();

        string redisConnectionString =
            builder.Configuration.GetConnectionString(authOptions.SessionTicketsConnectionStringName)
            ?? "localhost:6379";

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = authOptions.SessionTicketCacheInstanceName;
        });
        builder.Services.AddSingleton<RedisTicketStore>();
        builder.Services.AddRedisTicketStore();
        // Central API auth boundary: endpoint authorization failures on /api
        // return 401/403 instead of invoking browser OIDC redirects.
        builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationMiddlewareResultHandler>();

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieScheme;
                options.DefaultSignInScheme = CookieScheme;
                options.DefaultScheme = CookieScheme;
                options.DefaultChallengeScheme = OpenIdConnectScheme;
                options.DefaultSignOutScheme = OpenIdConnectScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = authOptions.CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = authOptions.LoginPath;
                options.LogoutPath = authOptions.LogoutPath;
                options.AccessDeniedPath = authOptions.AccessDeniedPath;
                options.SlidingExpiration = true;
                // Scheme-level fallback for direct cookie challenge/forbid
                // calls. The authorization result handler handles normal
                // endpoint policy failures before a scheme is challenged.
                options.Events.OnRedirectToLogin = context =>
                {
                    if (IsApiRequest(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (IsApiRequest(context.Request))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            })
            .AddOpenIdConnect(OpenIdConnectScheme, options =>
            {
                options.Authority = authOptions.Oidc.Authority;
                options.ClientId = authOptions.Oidc.ClientId;
                options.ClientSecret = authOptions.Oidc.ClientSecret;
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.CallbackPath = authOptions.Oidc.CallbackPath;
                options.SignedOutCallbackPath = authOptions.Oidc.SignedOutCallbackPath;
                options.SaveTokens = true;
                options.UsePkce = true;
                options.RequireHttpsMetadata = authOptions.Oidc.RequireHttpsMetadata;
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Events.OnTokenValidated = context =>
                {
                    if (context.Principal?.Identity is ClaimsIdentity identity
                        && !identity.HasClaim(claim => claim.Type == AppSessionClaimTypes.Provider))
                    {
                        string? provider = context.SecurityToken?.Issuer
                            ?? context.Options.Configuration?.Issuer
                            ?? context.Options.Authority;
                        if (!string.IsNullOrWhiteSpace(provider))
                        {
                            identity.AddClaim(new Claim(AppSessionClaimTypes.Provider, provider));
                        }
                    }

                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();

        return builder;
    }

    public static IServiceCollection AddRedisTicketStore(this IServiceCollection services)
    {
        services.ConfigureOptions<RedisTicketStoreCookieOptions>();

        return services;
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        return request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class HostAuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string CookieName { get; init; } = "ModularTemplate.Session";

    public string LoginPath { get; init; } = "/auth/login";

    public string LogoutPath { get; init; } = "/auth/logout";

    public string AccessDeniedPath { get; init; } = "/auth/access-denied";

    public string SessionTicketsConnectionStringName { get; init; } = "session-tickets";

    public string SessionTicketCacheInstanceName { get; init; } = "ModularTemplate.SessionTickets:";

    public OidcOptions Oidc { get; init; } = new();
}

public sealed class OidcOptions
{
    public string Authority { get; init; } = "http://localhost:8080/realms/modular-template";

    public string ClientId { get; init; } = "modular-template-host";

    public string? ClientSecret { get; init; }

    public string CallbackPath { get; init; } = "/auth/callback";

    public string SignedOutCallbackPath { get; init; } = "/auth/signed-out";

    public bool RequireHttpsMetadata { get; init; }
}
