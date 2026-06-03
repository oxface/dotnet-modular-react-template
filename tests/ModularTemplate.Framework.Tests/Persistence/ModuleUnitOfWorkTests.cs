using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Bondstone.Commands;
using ModularTemplate.Identity.Infrastructure.Persistence;
using ModularTemplate.Identity.Users;
using ModularTemplate.Identity.Users.Events;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Messaging;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.EntityFrameworkCore.Postgres.Persistence;
using Bondstone;
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
            new TestModulePersistenceResolver(identityUnitOfWork, operationsUnitOfWork));

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
            new TestModulePersistenceResolver(identityUnitOfWork));

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
            new TestModulePersistenceResolver(
                new TestModuleUnitOfWork("identity"),
                new TestModuleUnitOfWork("operations")));

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
            new TestModulePersistenceResolver(identityUnitOfWork));

        IModuleUnitOfWork? unitOfWork = resolver.Resolve(typeof(TestCommand));

        unitOfWork.ShouldBeSameAs(identityUnitOfWork);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StartModuleScope_WhenSameModuleIsNested_PreservesCurrentModule()
    {
        var context = new ModuleUnitOfWorkContext();

        using (context.StartModuleScope("identity"))
        {
            using (context.StartModuleScope("identity"))
            {
                context.CurrentModuleName.ShouldBe("identity");
            }

            context.CurrentModuleName.ShouldBe("identity");
        }

        context.CurrentModuleName.ShouldBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StartModuleScope_WhenDifferentModuleIsNested_ThrowsBeforeChangingCurrentModule()
    {
        var context = new ModuleUnitOfWorkContext();

        using (context.StartModuleScope("identity"))
        {
            InvalidOperationException exception = Should.Throw<InvalidOperationException>(
                () => context.StartModuleScope("operations"));

            exception.Message.ShouldContain("identity");
            exception.Message.ShouldContain("operations");
            exception.Message.ShouldContain("durable messaging");
            context.CurrentModuleName.ShouldBe("identity");
        }

        context.CurrentModuleName.ShouldBeNull();
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
        services.Any(service => service.ServiceType == typeof(IModulePersistenceResolver))
            .ShouldBeTrue();
        services.Any(service => service.ServiceType == typeof(OutboxWriter<IdentityDbContext>))
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenCommandHandlerAssemblyMarkerIsProvided_RegistersHandledCommands()
    {
        var services = new ServiceCollection();

        services.AddModulePersistence<IdentityDbContext>(
            "identity",
            ModuleCommandTypes.FromHandlerAssemblyMarkers(typeof(TestCommandHandler)));

        ModulePersistenceRegistration registration = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModulePersistenceRegistration>()
            .Single();
        registration.CommandTypes.ShouldBe([typeof(TestCommand)], ignoreOrder: true);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenCalledTwice_DoesNotDuplicateRuntimeAdapters()
    {
        var services = new ServiceCollection();

        services.AddModulePersistence<IdentityDbContext>("identity");
        services.AddModulePersistence<IdentityDbContext>("identity");

        services.Count(service => service.ServiceType == typeof(IModulePersistenceResolver))
            .ShouldBe(1);
        services.Count(service => service.ServiceType == typeof(OutboxWriter<IdentityDbContext>))
            .ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleCommandExecutor_WhenHandlerAndPipelineAreRegistered_ExecutesPipelineThenHandler()
    {
        var services = new ServiceCollection();
        var recorder = new CommandPipelineRecorder();
        services.AddSingleton(recorder);
        services.AddModuleCommands(options =>
        {
            options.AssemblyMarkers.Add(typeof(TestCommandHandler));
            options.PipelineBehaviors.Add(typeof(RecordingModuleCommandBehavior<,>));
        });

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IModuleCommandExecutor<TestCommand, TestResult> commandExecutor =
            serviceProvider.GetRequiredService<IModuleCommandExecutor<TestCommand, TestResult>>();

        TestResult result = await commandExecutor.SendAsync(new TestCommand(), CancellationToken.None);

        result.ShouldBe(TestResult.Value);
        recorder.Events.ShouldBe(["before", "handler", "after"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleCommandExecutor_WhenMultipleHandlersAreRegistered_Throws()
    {
        var services = new ServiceCollection();
        services.AddModuleCommands();
        services.AddScoped<IModuleCommandHandler<TestCommand, TestResult>>(_ => new TestCommandHandler());
        services.AddScoped<IModuleCommandHandler<TestCommand, TestResult>>(_ => new TestCommandHandler());

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IModuleCommandExecutor<TestCommand, TestResult> commandExecutor =
            serviceProvider.GetRequiredService<IModuleCommandExecutor<TestCommand, TestResult>>();

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await commandExecutor.SendAsync(new TestCommand(), CancellationToken.None));

        exception.Message.ShouldContain("Multiple module command handlers");
        exception.Message.ShouldContain(typeof(TestCommand).FullName!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleCommandBus_WhenHandlerAndPipelineAreRegistered_DelegatesToTypedExecutor()
    {
        var services = new ServiceCollection();
        var recorder = new CommandPipelineRecorder();
        services.AddSingleton(recorder);
        services.AddModuleCommands(options =>
        {
            options.AssemblyMarkers.Add(typeof(TestCommandHandler));
            options.PipelineBehaviors.Add(typeof(RecordingModuleCommandBehavior<,>));
        });

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        IModuleCommandBus commandBus = serviceProvider.GetRequiredService<IModuleCommandBus>();

        TestResult result = await commandBus.SendAsync(new TestCommand(), CancellationToken.None);

        result.ShouldBe(TestResult.Value);
        recorder.Events.ShouldBe(["before", "handler", "after"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenRuntimeAdaptersResolve_UsesSameScopedDbContext()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IMessageTypeRegistry>(new MessageTypeRegistry());
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=not_used"));
        services.AddModulePersistence<IdentityDbContext>("identity");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IdentityDbContext concreteContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        IModulePersistenceResolver persistenceResolver = scope.ServiceProvider.GetRequiredService<IModulePersistenceResolver>();
        IModuleDbContext moduleContext = persistenceResolver.ResolveDbContext("identity");
        IOutboxWriter outboxWriter = persistenceResolver.ResolveOutboxWriter("identity");
        persistenceResolver.ResolveUnitOfWork("identity")
            .ModuleName
            .ShouldBe("identity");
        OutboxMessage message = OutboxMessage.Create(
            messageId: Guid.NewGuid(),
            messageKind: MessageKind.Command,
            messageType: "test.command.v1",
            sourceModule: "identity",
            targetModule: "operations",
            correlationId: Guid.NewGuid(),
            causationId: null,
            operationId: null,
            payload: "{}");

        outboxWriter.Write(message);

        concreteContext.OutboxMessages.Local.Single().ShouldBeSameAs(message);
        moduleContext.OutboxMessages.Local.Single().ShouldBeSameAs(message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleBoundary_WhenModuleIsRegistered_ExecutesInsideResolvedUnitOfWork()
    {
        var unitOfWork = new TestModuleUnitOfWork("identity");
        var boundary = new EntityFrameworkCoreModuleBoundary([new TestModuleBoundaryExecutor(unitOfWork)]);

        int result = await boundary.ExecuteAsync(
            "identity",
            _ => new ValueTask<int>(42),
            CancellationToken.None);

        result.ShouldBe(42);
        unitOfWork.TransactionalExecutionCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModulePersistence_WhenRegisteredModuleDoesNotMatchDbContextModule_Throws()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IMessageTypeRegistry>(new MessageTypeRegistry());
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql("Host=localhost;Database=not_used"));
        services.AddModulePersistence<IdentityDbContext>("billing");

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        using IServiceScope scope = serviceProvider.CreateScope();
        IModulePersistenceResolver persistenceResolver = scope.ServiceProvider.GetRequiredService<IModulePersistenceResolver>();

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => persistenceResolver.ResolveDbContext("billing"));
        exception.Message.ShouldContain("billing");
        exception.Message.ShouldContain("identity");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddModuleMessaging_WhenHandlerAssemblyMarkerIsProvided_RegistersModuleMessagingMetadata()
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
        services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleEventSubscription>()
            .Single(subscription => subscription.EventType == typeof(TestIntegrationEvent))
            .ModuleName
            .ShouldBe("identity");
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
        services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleEventSubscription>()
            .Count(subscription => subscription.EventType == typeof(TestIntegrationEvent))
            .ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleUnitOfWorkBehavior_WhenCommandIsMapped_SavesResolvedUnitOfWork()
    {
        var unitOfWork = new TestModuleUnitOfWork("identity");
        var behavior = new ModuleUnitOfWorkCommandBehavior<TestCommand, TestResult>(
            new StaticModuleUnitOfWorkResolver(unitOfWork),
            new EntityFrameworkCoreModuleBoundary([new TestModuleBoundaryExecutor(unitOfWork)]));

        await behavior.HandleAsync(
            new TestCommand(),
            (_, _) => new ValueTask<TestResult>(TestResult.Value),
            CancellationToken.None);

        unitOfWork.TransactionalExecutionCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleUnitOfWorkBehavior_WhenCommandIsNotMapped_Throws()
    {
        var behavior = new ModuleUnitOfWorkCommandBehavior<TestCommand, TestResult>(
            new StaticModuleUnitOfWorkResolver(null),
            new ThrowingModuleBoundary());

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await behavior.HandleAsync(
                new TestCommand(),
                (_, _) => new ValueTask<TestResult>(TestResult.Value),
                CancellationToken.None));

        exception.Message.ShouldContain(typeof(TestCommand).FullName!);
        exception.Message.ShouldContain(nameof(NonPersistentCommandAttribute));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleUnitOfWorkBehavior_WhenCommandIsNonPersistent_RunsWithoutSave()
    {
        var behavior = new ModuleUnitOfWorkCommandBehavior<NonPersistentTestCommand, TestResult>(
            new StaticModuleUnitOfWorkResolver(null),
            new ThrowingModuleBoundary());

        TestResult result = await behavior.HandleAsync(
            new NonPersistentTestCommand(),
            (_, _) => new ValueTask<TestResult>(TestResult.Value),
            CancellationToken.None);

        result.ShouldBe(TestResult.Value);
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
            new StoredDomainEventMapper(),
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

    private sealed record TestResult
    {
        public static TestResult Value { get; } = new();
    }

    private sealed record TestCommand : IModuleCommand<TestResult>;

    private sealed record OtherCommand : IModuleCommand<TestResult>;

    [NonPersistentCommand]
    private sealed record NonPersistentTestCommand : IModuleCommand<TestResult>;

    private sealed class TestCommandHandler(CommandPipelineRecorder? recorder = null)
        : IModuleCommandHandler<TestCommand, TestResult>
    {
        public ValueTask<TestResult> HandleAsync(TestCommand command, CancellationToken cancellationToken)
        {
            recorder?.Events.Add("handler");
            return new ValueTask<TestResult>(TestResult.Value);
        }
    }

    private sealed class RecordingModuleCommandBehavior<TCommand, TResult>(
        CommandPipelineRecorder recorder)
        : IModuleCommandPipelineBehavior<TCommand, TResult>
        where TCommand : IModuleCommand<TResult>
    {
        public async ValueTask<TResult> HandleAsync(
            TCommand command,
            ModuleCommandHandlerDelegate<TCommand, TResult> next,
            CancellationToken cancellationToken)
        {
            recorder.Events.Add("before");
            TResult result = await next(command, cancellationToken);
            recorder.Events.Add("after");
            return result;
        }
    }

    private sealed class CommandPipelineRecorder
    {
        public List<string> Events { get; } = [];
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

    private sealed class TestModuleBoundaryExecutor(TestModuleUnitOfWork unitOfWork)
        : IModuleBoundaryExecutor
    {
        public string ModuleName => unitOfWork.ModuleName;

        public ValueTask<T> ExecuteAsync<T>(
            Func<CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken = default)
        {
            return unitOfWork.ExecuteTransactionalAsync(operation, cancellationToken);
        }
    }

    private sealed class TestModulePersistenceResolver(params IModuleUnitOfWork[] unitOfWorks)
        : IModulePersistenceResolver
    {
        public IModuleDbContext ResolveDbContext(string moduleName)
        {
            throw new NotSupportedException();
        }

        public IModuleUnitOfWork ResolveUnitOfWork(string moduleName)
        {
            return unitOfWorks.Single(unitOfWork =>
                string.Equals(unitOfWork.ModuleName, moduleName, StringComparison.Ordinal));
        }

        public IOutboxWriter ResolveOutboxWriter(string moduleName)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticModuleUnitOfWorkResolver(IModuleUnitOfWork? unitOfWork)
        : IModuleCommandBoundaryResolver
    {
        public string? ResolveModuleName(Type commandType)
        {
            return unitOfWork?.ModuleName;
        }
    }

    private sealed class ThrowingModuleBoundary : IModuleBoundary
    {
        public ValueTask ExecuteAsync(
            string moduleName,
            Func<CancellationToken, ValueTask> operation,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<T> ExecuteAsync<T>(
            string moduleName,
            Func<CancellationToken, ValueTask<T>> operation,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
