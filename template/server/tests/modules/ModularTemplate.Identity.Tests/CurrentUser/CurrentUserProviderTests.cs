using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Contracts.CurrentUser;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Tests.Support;
using Shouldly;

namespace ModularTemplate.Identity.Tests.CurrentUser;

public sealed class CurrentUserProviderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentUserAsync_WhenIdentityIsNew_CreatesLocalUserWithNoDefaultAccess()
    {
        var identityContext = new InMemoryIdentityContext();
        var handler = new SynchronizeCurrentUserCommandHandler(identityContext, identityContext);

        CurrentUserContext currentUser = await handler.HandleAsync(
            new SynchronizeCurrentUserCommand(new AuthenticatedIdentity("oidc", "subject-1", "Ada", "ada@example.test")),
            CancellationToken.None);

        currentUser.IsAuthenticated.ShouldBeTrue();
        currentUser.LocalUserId.ShouldNotBeNull();
        currentUser.DisplayName.ShouldBe("Ada");
        currentUser.Email.ShouldBe("ada@example.test");
        currentUser.HasApplicationAccess.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentUserAsync_WhenApplicationAccessIsActive_ReturnsAccessState()
    {
        var identityContext = new InMemoryIdentityContext();
        var handler = new SynchronizeCurrentUserCommandHandler(identityContext, identityContext);
        var identity = new AuthenticatedIdentity("oidc", "subject-1", "Ada", "ada@example.test");
        CurrentUserContext created = await handler.HandleAsync(
            new SynchronizeCurrentUserCommand(identity),
            CancellationToken.None);

        identityContext.Add(ApplicationAccess.GrantTo(created.LocalUserId!.Value));

        CurrentUserContext currentUser = await handler.HandleAsync(
            new SynchronizeCurrentUserCommand(identity),
            CancellationToken.None);

        currentUser.HasApplicationAccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCurrentUserAsync_WhenIdentityIsMissing_ReturnsUnauthenticated()
    {
        var identityContext = new InMemoryIdentityContext();
        var handler = new SynchronizeCurrentUserCommandHandler(identityContext, identityContext);

        CurrentUserContext currentUser = await handler.HandleAsync(
            new SynchronizeCurrentUserCommand(null),
            CancellationToken.None);

        currentUser.ShouldBe(CurrentUserContext.Unauthenticated);
    }
}
