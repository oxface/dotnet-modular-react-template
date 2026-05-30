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
using ModularTemplate.Infrastructure.Transport;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.Handlers;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModuleUnitOfWorkTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandTypeIsRegistered_ReturnsMatchingModuleUnitOfWork()
    {
        var identityUnitOfWork = new TestModuleUnitOfWork("identity");
        var operationsUnitOfWork = new TestModuleUnitOfWork("operations");
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand)])
            ],
            [identityUnitOfWork, operationsUnitOfWork]);

        IModuleUnitOfWork? unitOfWork = resolver.Resolve(typeof(TestCommand));

        unitOfWork.ShouldBeSameAs(identityUnitOfWork);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandTypeIsNotRegistered_ReturnsNull()
    {
        var identityUnitOfWork = new TestModuleUnitOfWork("identity");
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(OtherCommand)])
            ],
            [identityUnitOfWork]);

        IModuleUnitOfWork? unitOfWork = resolver.Resolve(typeof(TestCommand));

        unitOfWork.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandTypeIsRegisteredForMultipleModules_Throws()
    {
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand)]),
                new ModulePersistenceRegistration(
                    "operations",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand)])
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
    public void Resolve_WhenCommandTypeIsRegisteredForSameModuleMoreThanOnce_ReturnsMatchingModuleUnitOfWork()
    {
        var identityUnitOfWork = new TestModuleUnitOfWork("identity");
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand)]),
                new ModulePersistenceRegistration(
                    "identity",
                    typeof(IdentityDbContext),
                    [typeof(TestCommand)])
            ],
            [identityUnitOfWork]);

        IModuleUnitOfWork? unitOfWork = resolver.Resolve(typeof(TestCommand));

        unitOfWork.ShouldBeSameAs(identityUnitOfWork);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenNoCommandHandlerAssemblyMarkersAreProvided_RegistersModuleWithoutCommands()
    {
        var services = new ServiceCollection();

        services.AddModulePersistence<IdentityDbContext>("identity");

        services.Any(service =>
            service.ServiceType == typeof(ModulePersistenceRegistration)
            && service.ImplementationInstance is ModulePersistenceRegistration registration
            && registration.ModuleName == "identity"
            && registration.CommandTypes.Count == 0).ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenCommandHandlerAssemblyMarkerIsProvided_RegistersHandledCommands()
    {
        var services = new ServiceCollection();

        services.AddModulePersistence<IdentityDbContext>("identity", typeof(TestCommandHandler));

        ModulePersistenceRegistration registration = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModulePersistenceRegistration>()
            .Single();
        registration.CommandTypes.ShouldBe([typeof(TestCommand)], ignoreOrder: true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModuleMessaging_WhenHandlerAssemblyMarkerIsProvided_RegistersModuleScopedRebusAdapter()
    {
        var services = new ServiceCollection();

        services.AddModuleMessaging("identity", typeof(TestModuleMessageHandler));

        ModuleMessageHandlerRegistration registration = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleMessageHandlerRegistration>()
            .Single(registration => registration.HandlerType == typeof(TestModuleMessageHandler));
        registration.ModuleName.ShouldBe("identity");
        registration.MessageType.ShouldBe(typeof(TestIntegrationEvent));
        registration.HandlerType.ShouldBe(typeof(TestModuleMessageHandler));
        registration.MessageIdentity.ShouldBe("test.integration-event.v1");
        services.Any(service =>
            service.ServiceType == typeof(IHandleMessages<TestIntegrationEvent>)
            && service.ImplementationType == typeof(ModuleScopedRebusHandler<TestIntegrationEvent>))
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModuleMessaging_WhenCalledWithOverlappingAssemblies_DoesNotDuplicateHandlerRegistrations()
    {
        var services = new ServiceCollection();

        services.AddModuleMessaging("identity", typeof(TestModuleMessageHandler));
        services.AddModuleMessaging("identity", typeof(TestModuleMessageHandler));

        services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleMessageHandlerRegistration>()
            .Count(registration => registration.HandlerType == typeof(TestModuleMessageHandler))
            .ShouldBe(1);
        services.Count(service =>
                service.ServiceType == typeof(IHandleMessages<TestIntegrationEvent>)
                && service.ImplementationType == typeof(ModuleScopedRebusHandler<TestIntegrationEvent>))
            .ShouldBe(1);
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

        unitOfWork.TransactionalExecutionCount.ShouldBe(1);
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
            new ModuleUnitOfWorkContext(),
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

    private sealed record OtherCommand : ICommand;

    private sealed class TestCommandHandler : ICommandHandler<TestCommand>
    {
        public ValueTask<Unit> Handle(TestCommand command, CancellationToken cancellationToken)
        {
            return new ValueTask<Unit>(Unit.Value);
        }
    }

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

    private sealed class TestModuleMessageHandler : IModuleMessageHandler<TestIntegrationEvent>
    {
        public Task HandleAsync(TestIntegrationEvent message, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestModuleUnitOfWork(string moduleName) : IModuleUnitOfWork
    {
        public string ModuleName { get; } = moduleName;

        public int TransactionalExecutionCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async ValueTask<T> ExecuteTransactionalAsync<T>(
            Func<CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken = default)
        {
            TransactionalExecutionCount++;
            return await operation(cancellationToken);
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
