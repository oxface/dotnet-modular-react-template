using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModularTemplate.Host.Authentication;
using ModularTemplate.Host.Configuration;
using Shouldly;

namespace ModularTemplate.Host.Tests.Authentication;

public sealed class HostAuthenticationConfigurationTests
{
    [Fact]
    [Trait("Category", "Application")]
    public void Host_authentication_uses_cookie_session_and_oidc_challenge_schemes()
    {
        using ServiceProvider services = BuildServices();

        AuthenticationOptions authOptions = services.GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value;
        CookieAuthenticationOptions cookieOptions =
            services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(HostAuthenticationConfiguration.CookieScheme);
        OpenIdConnectOptions oidcOptions =
            services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(HostAuthenticationConfiguration.OpenIdConnectScheme);

        authOptions.DefaultAuthenticateScheme.ShouldBe(HostAuthenticationConfiguration.CookieScheme);
        authOptions.DefaultChallengeScheme.ShouldBe(HostAuthenticationConfiguration.OpenIdConnectScheme);
        authOptions.DefaultSignOutScheme.ShouldBe(HostAuthenticationConfiguration.OpenIdConnectScheme);
        cookieOptions.SessionStore.ShouldBeOfType<RedisTicketStore>();
        oidcOptions.Authority.ShouldBe("http://localhost:8080/realms/modular-template");
        oidcOptions.ClientId.ShouldBe("modular-template-host");
        oidcOptions.CallbackPath.ToString().ShouldBe("/auth/callback");
        oidcOptions.SignedOutCallbackPath.ToString().ShouldBe("/auth/signed-out");
        oidcOptions.UsePkce.ShouldBeTrue();
        oidcOptions.SaveTokens.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Application")]
    public async Task Oidc_token_validation_stamps_configured_authority_as_provider()
    {
        using ServiceProvider services = BuildServices();
        OpenIdConnectOptions oidcOptions =
            services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>()
                .Get(HostAuthenticationConfiguration.OpenIdConnectScheme);
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "subject-1")], "oidc"));
        var context = new TokenValidatedContext(
            new DefaultHttpContext { RequestServices = services },
            new AuthenticationScheme(
                HostAuthenticationConfiguration.OpenIdConnectScheme,
                HostAuthenticationConfiguration.OpenIdConnectScheme,
                typeof(OpenIdConnectHandler)),
            oidcOptions,
            principal,
            new AuthenticationProperties());

        await oidcOptions.Events.TokenValidated(context);

        principal.FindFirst("provider")?.Value
            .ShouldBe("http://localhost:8080/realms/modular-template");
    }

    private static ServiceProvider BuildServices()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Authentication:Oidc:Authority"] =
            "http://localhost:8080/realms/modular-template";
        builder.AddHostAuthentication();
        builder.Services.RemoveAll<IDistributedCache>();
        builder.Services.AddDistributedMemoryCache();

        return builder.Services.BuildServiceProvider();
    }
}
