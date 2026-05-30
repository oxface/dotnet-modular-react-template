using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Identity.Users;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class IdentityRepositoryTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetByProviderSubjectAsync_WhenProviderSubjectExists_ReusesExistingUser()
    {
        await using IdentityDbContext identityContext = CreateIdentityDbContext();
        await identityContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await identityContext.Database.EnsureCreatedAsync(CancellationToken.None);
        var users = new LocalUserRepository(identityContext);
        LocalUser first = LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test");
        users.Add(first);
        await identityContext.SaveChangesAsync(CancellationToken.None);

        LocalUser? second = await users.GetByProviderSubjectAsync(
            "oidc",
            "subject-1",
            CancellationToken.None);

        second.ShouldNotBeNull();
        second.Id.ShouldBe(first.Id);
        (await identityContext.LocalUsers.CountAsync(CancellationToken.None)).ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HandleAsync_WhenApplicationAccessIsActive_ReturnsTrue()
    {
        await using IdentityDbContext identityContext = CreateIdentityDbContext();
        await identityContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await identityContext.Database.EnsureCreatedAsync(CancellationToken.None);
        var users = new LocalUserRepository(identityContext);
        var accessRepository = new ApplicationAccessRepository(identityContext);
        LocalUser user = LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test");
        users.Add(user);
        accessRepository.Add(ApplicationAccess.GrantTo(user.Id));
        await identityContext.SaveChangesAsync(CancellationToken.None);

        bool hasAccess = await accessRepository.HasActiveAccessAsync(user.Id, CancellationToken.None);

        hasAccess.ShouldBeTrue();
    }

    private IdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }
}
