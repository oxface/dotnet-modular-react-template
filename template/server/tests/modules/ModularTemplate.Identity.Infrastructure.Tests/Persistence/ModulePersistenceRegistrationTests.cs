using Mediator;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.Identity.Access;
using ModularTemplate.Identity.CurrentUser;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Persistence.Transactions;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class ModulePersistenceRegistrationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandTypeIsExplicitlyMapped_ReturnsMatchingUnitOfWork()
    {
        var identityUnitOfWork = new TestUnitOfWork("identity");
        var operationsUnitOfWork = new TestUnitOfWork("operations");
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration("identity", typeof(object), [typeof(TestIdentityCommand)]),
                new ModulePersistenceRegistration("operations", typeof(object), [typeof(TestOperationsCommand)]),
            ],
            [identityUnitOfWork, operationsUnitOfWork]);

        IModuleUnitOfWork? result = resolver.Resolve(typeof(TestIdentityCommand));

        result.ShouldBeSameAs(identityUnitOfWork);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandSharesAssemblyButIsNotMapped_ReturnsNull()
    {
        var resolver = new ModuleUnitOfWorkResolver(
            [new ModulePersistenceRegistration("identity", typeof(object), [typeof(TestIdentityCommand)])],
            [new TestUnitOfWork("identity")]);

        IModuleUnitOfWork? result = resolver.Resolve(typeof(UnmappedCommand));

        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Resolve_WhenCommandIsMappedToMultipleModules_Throws()
    {
        var resolver = new ModuleUnitOfWorkResolver(
            [
                new ModulePersistenceRegistration("identity", typeof(object), [typeof(TestIdentityCommand)]),
                new ModulePersistenceRegistration("operations", typeof(object), [typeof(TestIdentityCommand)]),
            ],
            [new TestUnitOfWork("identity"), new TestUnitOfWork("operations")]);

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => resolver.Resolve(typeof(TestIdentityCommand)));

        exception.Message.ShouldContain("mapped to more than one module");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WhenMappedTypeIsNotACommand_Throws()
    {
        ArgumentException exception = Should.Throw<ArgumentException>(
            () => new ModulePersistenceRegistration("identity", typeof(object), [typeof(string)]));

        exception.Message.ShouldContain("must implement IBaseCommand");
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

        services.AddModulePersistence<IdentityDbContext>("identity", typeof(TestIdentityCommandHandler));

        ModulePersistenceRegistration registration = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModulePersistenceRegistration>()
            .Single();
        registration.CommandTypes.ShouldBe([typeof(TestIdentityCommand)], ignoreOrder: true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenIdentityHandlerAssemblyIsRegistered_RegistersIdentityCommands()
    {
        var services = new ServiceCollection();

        services.AddModulePersistence<IdentityDbContext>("identity", typeof(GrantInitialAdminAccessCommandHandler));

        ModulePersistenceRegistration registration = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModulePersistenceRegistration>()
            .Single();
        registration.CommandTypes.ShouldBe(
            [typeof(GrantInitialAdminAccessCommand), typeof(SynchronizeCurrentUserCommand)],
            ignoreOrder: true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenUnitOfWorkIsResolved_RunsHandlerInsideTransaction()
    {
        var unitOfWork = new TestUnitOfWork("identity");
        var behavior = new ModuleUnitOfWorkBehavior<TestIdentityCommand, string>(
            new TestUnitOfWorkResolver(unitOfWork));
        bool handlerWasCalledInsideTransaction = false;

        string result = await behavior.Handle(
            new TestIdentityCommand(),
            (_, _) =>
            {
                handlerWasCalledInsideTransaction = unitOfWork.IsExecutingTransaction;
                return new ValueTask<string>("handled");
            },
            CancellationToken.None);

        result.ShouldBe("handled");
        handlerWasCalledInsideTransaction.ShouldBeTrue();
        unitOfWork.ExecuteTransactionalCallCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Handle_WhenUnitOfWorkIsNotResolved_RunsHandlerWithoutTransaction()
    {
        var behavior = new ModuleUnitOfWorkBehavior<TestIdentityCommand, string>(
            new TestUnitOfWorkResolver(null));
        bool handlerWasCalled = false;

        string result = await behavior.Handle(
            new TestIdentityCommand(),
            (_, _) =>
            {
                handlerWasCalled = true;
                return new ValueTask<string>("handled");
            },
            CancellationToken.None);

        result.ShouldBe("handled");
        handlerWasCalled.ShouldBeTrue();
    }

    private sealed record TestIdentityCommand : ICommand<string>;

    private sealed record TestOperationsCommand : ICommand<string>;

    private sealed record UnmappedCommand : ICommand<string>;

    private sealed class TestIdentityCommandHandler : ICommandHandler<TestIdentityCommand, string>
    {
        public ValueTask<string> Handle(TestIdentityCommand command, CancellationToken cancellationToken)
        {
            return new ValueTask<string>("handled");
        }
    }

    private sealed class TestUnitOfWork(string moduleName) : IModuleUnitOfWork
    {
        public string ModuleName { get; } = moduleName;

        public bool IsExecutingTransaction { get; private set; }

        public int ExecuteTransactionalCallCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public async ValueTask<T> ExecuteTransactionalAsync<T>(
            Func<CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken = default)
        {
            ExecuteTransactionalCallCount++;
            IsExecutingTransaction = true;

            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                IsExecutingTransaction = false;
            }
        }
    }

    private sealed class TestUnitOfWorkResolver(IModuleUnitOfWork? unitOfWork) : IModuleUnitOfWorkResolver
    {
        public IModuleUnitOfWork? Resolve(Type commandType)
        {
            return unitOfWork;
        }
    }
}
