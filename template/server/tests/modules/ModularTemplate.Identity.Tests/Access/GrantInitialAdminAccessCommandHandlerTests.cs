using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Tests.Support;
using Shouldly;

namespace ModularTemplate.Identity.Tests.Access;

public sealed class GrantInitialAdminAccessCommandHandlerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenConfigurationIsComplete_CreatesActiveApplicationAccess()
    {
        var identity = new InMemoryIdentityContext();
        var handler = new GrantInitialAdminAccessCommandHandler(identity, identity);

        GrantInitialAdminAccessResult result = await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: false),
            CancellationToken.None);
        var user = await identity.GetByProviderSubjectAsync(
            "oidc",
            "subject-1",
            CancellationToken.None);

        bool hasAccess = await identity.HasActiveAccessAsync(user!.Id, CancellationToken.None);

        result.ShouldBe(GrantInitialAdminAccessResult.Granted);
        hasAccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenRunRepeatedly_IsIdempotent()
    {
        var identity = new InMemoryIdentityContext();
        var handler = new GrantInitialAdminAccessCommandHandler(identity, identity);

        await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: false),
            CancellationToken.None);
        GrantInitialAdminAccessResult result = await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: false),
            CancellationToken.None);

        result.ShouldBe(GrantInitialAdminAccessResult.AlreadyGranted);
        identity.ApplicationAccess.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenAccessWasRevoked_DoesNotReactivateWithoutForce()
    {
        var identity = new InMemoryIdentityContext();
        var handler = new GrantInitialAdminAccessCommandHandler(identity, identity);
        await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: false),
            CancellationToken.None);
        identity.ApplicationAccess.Single().Revoke();

        GrantInitialAdminAccessResult result = await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: false),
            CancellationToken.None);

        result.ShouldBe(GrantInitialAdminAccessResult.RevokedRequiresForce);
        identity.ApplicationAccess.Single().IsActive.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenAccessWasRevokedAndForceIsSet_ReactivatesAccess()
    {
        var identity = new InMemoryIdentityContext();
        var handler = new GrantInitialAdminAccessCommandHandler(identity, identity);
        await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: false),
            CancellationToken.None);
        identity.ApplicationAccess.Single().Revoke();

        GrantInitialAdminAccessResult result = await handler.HandleAsync(
            new GrantInitialAdminAccessCommand("oidc", "subject-1", Force: true),
            CancellationToken.None);

        result.ShouldBe(GrantInitialAdminAccessResult.Granted);
        identity.ApplicationAccess.Single().IsActive.ShouldBeTrue();
    }
}
