using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Infrastructure.Tests.Support;
using ModularTemplate.Identity.Users;
using ModularTemplate.Identity.Users.Events;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModuleUnitOfWorkIntegrationTests(PostgreSqlFixture postgreSqlFixture)
    : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task SaveChangesAsync_WhenCalledInsideTransaction_ClearsEventsAfterEachSuccessfulFlush()
    {
        await using IdentityDbContext dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        await using ServiceProvider serviceProvider = CreateServiceProvider();
        ModuleUnitOfWork<IdentityDbContext> unitOfWork = CreateUnitOfWork(dbContext, serviceProvider);
        Guid localUserId = Guid.Empty;

        await unitOfWork.ExecuteTransactionalAsync(
            async ct =>
            {
                LocalUser localUser = LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test");
                localUserId = localUser.Id;
                dbContext.LocalUsers.Add(localUser);
                localUser.DomainEvents.Count.ShouldBe(1);

                await unitOfWork.SaveChangesAsync(ct);

                localUser.DomainEvents.Count.ShouldBe(0);
                (await dbContext.DomainEvents.CountAsync(ct)).ShouldBe(1);

                localUser.MarkSeen("Ada Lovelace", "ada@example.test");
                localUser.DomainEvents.Count.ShouldBe(1);

                await unitOfWork.SaveChangesAsync(ct);

                localUser.DomainEvents.Count.ShouldBe(0);
                (await dbContext.DomainEvents.CountAsync(ct)).ShouldBe(2);
                (await dbContext.OutboxMessages.CountAsync(ct)).ShouldBe(2);
                return true;
            },
            CancellationToken.None);

        await using IdentityDbContext verifyContext = CreateDbContext();
        (await verifyContext.LocalUsers.CountAsync(CancellationToken.None)).ShouldBe(1);
        (await verifyContext.DomainEvents.CountAsync(CancellationToken.None)).ShouldBe(2);
        (await verifyContext.OutboxMessages.CountAsync(CancellationToken.None)).ShouldBe(2);
        Guid[] correlationIds = await verifyContext.OutboxMessages
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => message.CorrelationId)
            .ToArrayAsync(CancellationToken.None);
        correlationIds.Distinct().Count().ShouldBe(1);
        string[] partitionKeys = await verifyContext.OutboxMessages
            .OrderBy(message => message.CreatedAtUtc)
            .Select(message => message.PartitionKey!)
            .ToArrayAsync(CancellationToken.None);
        partitionKeys.Distinct().ShouldBe([$"identity.local-user:{localUserId:D}"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExecuteTransactionalAsync_WhenTransactionFailsAfterMidFlush_RollsBackFlushedRows()
    {
        await using IdentityDbContext dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync(CancellationToken.None);
        await using ServiceProvider serviceProvider = CreateServiceProvider();
        ModuleUnitOfWork<IdentityDbContext> unitOfWork = CreateUnitOfWork(dbContext, serviceProvider);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await unitOfWork.ExecuteTransactionalAsync<bool>(
                async ct =>
                {
                    LocalUser localUser = LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test");
                    dbContext.LocalUsers.Add(localUser);
                    await unitOfWork.SaveChangesAsync(ct);

                    throw new InvalidOperationException("phase two failed");
                },
                CancellationToken.None));

        exception.Message.ShouldContain("phase two failed");

        await using IdentityDbContext verifyContext = CreateDbContext();
        (await verifyContext.LocalUsers.CountAsync(CancellationToken.None)).ShouldBe(0);
        (await verifyContext.DomainEvents.CountAsync(CancellationToken.None)).ShouldBe(0);
        (await verifyContext.OutboxMessages.CountAsync(CancellationToken.None)).ShouldBe(0);

        InvalidOperationException reuseException = await Should.ThrowAsync<InvalidOperationException>(
            async () => await unitOfWork.SaveChangesAsync(CancellationToken.None));
        reuseException.Message.ShouldContain("cannot be reused");
    }

    private IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(postgreSqlFixture.ConnectionString)
            .Options;

        return new IdentityDbContext(options);
    }

    private static ModuleUnitOfWork<IdentityDbContext> CreateUnitOfWork(
        IdentityDbContext dbContext,
        IServiceProvider serviceProvider)
    {
        var messageTypeRegistry = new MessageTypeRegistry();
        messageTypeRegistry.Register<TestIntegrationEvent>();

        return new ModuleUnitOfWork<IdentityDbContext>(
            dbContext,
            serviceProvider,
            messageTypeRegistry,
            new StoredDomainEventMapper(),
            new ModuleUnitOfWorkContext(),
            Options.Create(new DurableMessagingOptions()));
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddSingleton<IIntegrationEventMapper<LocalUserCreatedDomainEvent>, LocalUserCreatedIntegrationEventMapper>()
            .AddSingleton<IIntegrationEventMapper<LocalUserSeenDomainEvent>, LocalUserSeenIntegrationEventMapper>()
            .BuildServiceProvider();
    }

    [IntegrationEventIdentity("test.local-user-changed.v1")]
    private sealed record TestIntegrationEvent(Guid LocalUserId, string Change) : IIntegrationEvent;

    private sealed class LocalUserCreatedIntegrationEventMapper
        : IIntegrationEventMapper<LocalUserCreatedDomainEvent>
    {
        public string SourceModule => "identity";

        public IReadOnlyCollection<IIntegrationEvent> Map(LocalUserCreatedDomainEvent domainEvent)
        {
            return [new TestIntegrationEvent(domainEvent.LocalUserId, "created")];
        }
    }

    private sealed class LocalUserSeenIntegrationEventMapper
        : IIntegrationEventMapper<LocalUserSeenDomainEvent>
    {
        public string SourceModule => "identity";

        public IReadOnlyCollection<IIntegrationEvent> Map(LocalUserSeenDomainEvent domainEvent)
        {
            return [new TestIntegrationEvent(domainEvent.LocalUserId, "seen")];
        }
    }
}
