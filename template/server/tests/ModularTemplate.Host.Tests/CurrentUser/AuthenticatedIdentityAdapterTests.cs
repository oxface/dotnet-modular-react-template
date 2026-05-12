using System.Security.Claims;
using ModularTemplate.Host.Features.CurrentUser;
using Shouldly;

namespace ModularTemplate.Host.Tests.CurrentUser;

public sealed class AuthenticatedIdentityAdapterTests
{
    [Fact]
    [Trait("Category", "Application")]
    public void FromClaimsPrincipal_uses_explicit_provider_claim()
    {
        var principal = CreatePrincipal(
            new Claim("provider", "http://localhost:8080/realms/modular-template"),
            new Claim("iss", "http://issuer-from-token.example.test"),
            new Claim(ClaimTypes.NameIdentifier, "subject-1"));

        var identity = AuthenticatedIdentityAdapter.FromClaimsPrincipal(principal);

        identity.ShouldNotBeNull();
        identity.Provider.ShouldBe("http://localhost:8080/realms/modular-template");
        identity.Subject.ShouldBe("subject-1");
    }

    [Fact]
    [Trait("Category", "Application")]
    public void FromClaimsPrincipal_returns_null_when_provider_is_missing()
    {
        var principal = CreatePrincipal(new Claim(ClaimTypes.NameIdentifier, "subject-1"));

        var identity = AuthenticatedIdentityAdapter.FromClaimsPrincipal(principal);

        identity.ShouldBeNull();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }
}
