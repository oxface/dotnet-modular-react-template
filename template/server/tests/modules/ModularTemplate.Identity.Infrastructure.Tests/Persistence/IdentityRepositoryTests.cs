using Microsoft.EntityFrameworkCore;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Identity.Users;
using ModularTemplate.Infrastructure.Persistence.DomainEvents;
using ModularTemplate.SharedKernel.Domain;
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
    public async Task SaveChangesAsync_WhenAggregateHasDomainEvents_PersistsDomainEventRows()
    {
        await using IdentityDbContext identityContext = CreateIdentityDbContext();
        await identityContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await identityContext.Database.EnsureCreatedAsync(CancellationToken.None);

        var users = new LocalUserRepository(identityContext);
        LocalUser user = LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test");
        users.Add(user);

        // Simulate ModuleUnitOfWork: capture domain events before saving.
        IReadOnlyCollection<IDomainEvent> domainEvents = user.DequeueDomainEvents();
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            identityContext.DomainEvents.Add(
                StoredDomainEvent.FromDomainEvent(domainEvent, user.Id.ToString()));
        }

        await identityContext.SaveChangesAsync(CancellationToken.None);

        StoredDomainEvent storedEvent = await identityContext.DomainEvents.SingleAsync(CancellationToken.None);
        storedEvent.AggregateType.ShouldBe("identity.local-user");
        storedEvent.AggregateId.ShouldBe(user.Id.ToString());
        storedEvent.EventType.ShouldBe("identity.local-user-created");
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
