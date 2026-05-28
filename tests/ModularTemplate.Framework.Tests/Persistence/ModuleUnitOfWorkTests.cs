using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Users;
using ModularTemplate.Identity.Users.Events;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence.Transactions;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModuleUnitOfWorkTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandAssemblyIsRegistered_ReturnsMatchingModuleUnitOfWork()
    {
        var identityUnitOfWork = new TestModuleUnitOfWork("identity");
        var operationsUnitOfWork = new TestModuleUnitOfWork("operations");
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand).Assembly])
            ],
            [identityUnitOfWork, operationsUnitOfWork]);

        IModuleUnitOfWork? unitOfWork = resolver.Resolve(typeof(TestCommand));

        unitOfWork.ShouldBeSameAs(identityUnitOfWork);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandAssemblyIsNotRegistered_ReturnsNull()
    {
        var identityUnitOfWork = new TestModuleUnitOfWork("identity");
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(IdentityDbContext).Assembly])
            ],
            [identityUnitOfWork]);

        IModuleUnitOfWork? unitOfWork = resolver.Resolve(typeof(TestCommand));

        unitOfWork.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandAssemblyIsRegisteredForMultipleModules_Throws()
    {
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand).Assembly]),
                new ModulePersistenceRegistration(
                    "operations",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand).Assembly])
            ],
            [new TestModuleUnitOfWork("identity"), new TestModuleUnitOfWork("operations")]);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => resolver.Resolve(typeof(TestCommand)));

        exception.Message.ShouldContain("more than one module persistence registration");
        exception.Message.ShouldContain("identity");
        exception.Message.ShouldContain("operations");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenNoCommandAssemblyMarkersAreProvided_Throws()
    {
        var services = new ServiceCollection();

        ArgumentException exception = Should.Throw<ArgumentException>(
            () => services.AddModulePersistence<IdentityDbContext>("identity"));

        exception.ParamName.ShouldBe("commandAssemblyMarkers");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleUnitOfWorkBehavior_WhenCommandIsMapped_SavesResolvedUnitOfWork()
    {
        var unitOfWork = new TestModuleUnitOfWork("identity");
        var behavior = new ModuleUnitOfWorkBehavior<TestCommand, Unit>(
            new StaticModuleUnitOfWorkResolver(unitOfWork));

        await behavior.Handle(
            new TestCommand(),
            (_, _) => new ValueTask<Unit>(Unit.Value),
            CancellationToken.None);

        unitOfWork.TransactionalSaveCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleUnitOfWorkBehavior_WhenCommandIsNotMapped_DoesNotSave()
    {
        var behavior = new ModuleUnitOfWorkBehavior<TestCommand, Unit>(
            new StaticModuleUnitOfWorkResolver(null));

        Unit result = await behavior.Handle(
            new TestCommand(),
            (_, _) => new ValueTask<Unit>(Unit.Value),
            CancellationToken.None);

        result.ShouldBe(Unit.Value);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SaveChangesAsync_WhenIntegrationMapperUsesDifferentSourceModule_Throws()
    {
        await using var identityContext = CreateIdentityContext();
        await using ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IIntegrationEventMapper<LocalUserCreatedDomainEvent>>(
                new WrongSourceIntegrationEventMapper())
            .BuildServiceProvider();
        var registry = new MessageTypeRegistry();
        registry.Register<TestIntegrationEvent>();
        var unitOfWork = new ModuleUnitOfWork<IdentityDbContext>(
            identityContext,
            serviceProvider,
            registry,
            Options.Create(new DurableMessagingOptions()));
        identityContext.LocalUsers.Add(
            LocalUser.Create("oidc", "subject-1", "Ada", "ada@example.test"));

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await unitOfWork.SaveChangesAsync(CancellationToken.None));

        exception.Message.ShouldContain("operations");
        exception.Message.ShouldContain("identity");
    }

    private static IdentityDbContext CreateIdentityContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql("Host=localhost;Database=not_used")
            .Options;

        return new IdentityDbContext(options);
    }

    private sealed record TestCommand : ICommand;

    [MessageIdentity("test.integration-event.v1")]
    private sealed record TestIntegrationEvent(Guid LocalUserId) : IIntegrationEvent;

    private sealed class WrongSourceIntegrationEventMapper
        : IIntegrationEventMapper<LocalUserCreatedDomainEvent>
    {
        public string SourceModule => "operations";

        public IReadOnlyCollection<IIntegrationEvent> Map(LocalUserCreatedDomainEvent domainEvent)
        {
            return [new TestIntegrationEvent(domainEvent.LocalUserId)];
        }
    }

    private sealed class TestModuleUnitOfWork(string moduleName) : IModuleUnitOfWork
    {
        public string ModuleName { get; } = moduleName;

        public int TransactionalSaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task SaveChangesTransactionalAsync(CancellationToken cancellationToken = default)
        {
            TransactionalSaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StaticModuleUnitOfWorkResolver(IModuleUnitOfWork? unitOfWork)
        : IModuleUnitOfWorkResolver
    {
        public IModuleUnitOfWork? Resolve(Type commandType)
        {
            commandType.ShouldBe(typeof(TestCommand));

            return unitOfWork;
        }
    }
}
