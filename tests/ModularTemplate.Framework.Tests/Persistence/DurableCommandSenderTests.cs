using System.Text.Json;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.Messaging;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Transport.Rebus;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class DurableCommandSenderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenCommandIsAccepted_WritesOutboxMessageAndReturnsSubmission()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(outboxWriter),
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());
        Guid durableOperationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid causationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var command = new RebuildOperationProjectionCommand(durableOperationId);

        CommandSubmission submission;
        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            submission = sender.Send(
                command,
                targetModule: "products",
                durableOperationId: durableOperationId,
                causationId: causationId);
        }

        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
        submission.DurableOperationId.ShouldBe(durableOperationId);
        OutboxMessage message = outboxWriter.Messages.Single();
        message.MessageId.ShouldBe(submission.SubmissionId);
        message.MessageKind.ShouldBe(MessageKind.Command);
        message.MessageType.ShouldBe("products.rebuild-operation-projection.v1");
        message.SourceModule.ShouldBe("identity");
        message.TargetModule.ShouldBe("products");
        message.CorrelationId.ShouldBe(submission.SubmissionId);
        message.CausationId.ShouldBe(causationId);
        message.DurableOperationId.ShouldBe(durableOperationId);
        message.MaxAttempts.ShouldBe(7);
        message.Metadata.ShouldBeNull();
        JsonSerializer.Deserialize<RebuildOperationProjectionCommand>(message.Payload)
            .ShouldBe(command);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenActivityIsActive_UsesActivityTraceAndBaggage()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(outboxWriter),
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());
        Guid causationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid durableOperationId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        CommandSubmission submission;
        using Activity activity = new Activity("test command")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();
        activity.AddBaggage(BondstoneDiagnostics.CausationIdBaggageKey, causationId.ToString("D"));
        activity.AddBaggage(BondstoneDiagnostics.DurableOperationIdBaggageKey, durableOperationId.ToString("D"));

        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            submission = sender.Send(
                new RebuildOperationProjectionCommand(durableOperationId),
                targetModule: "products");
        }

        submission.DurableOperationId.ShouldBe(durableOperationId);
        OutboxMessage message = outboxWriter.Messages.Single();
        message.CorrelationId.ShouldBe(BondstoneDiagnostics.CreateCorrelationId(activity).ShouldNotBeNull());
        message.CausationId.ShouldBe(causationId);
        message.DurableOperationId.ShouldBe(durableOperationId);
        message.MessageId.ShouldBe(submission.SubmissionId);
        MessageTraceContext.FromMetadata(message.Metadata)
            ?.TraceParent
            .ShouldBe(activity.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenSourceModuleHasNoOutboxWriter_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(new CapturingOutboxWriter("identity")),
            HandlerRegistrations("identity"),
            registry,
            unitOfWorkContext,
            MessagingOptions());

        using (unitOfWorkContext.StartModuleScope("products"))
        {
            Should.Throw<InvalidOperationException>(
                () => sender.Send(
                    new RebuildOperationProjectionCommand(Guid.NewGuid()),
                    targetModule: "identity"));
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenNoModuleUnitOfWorkIsActive_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var sender = new DurableCommandSender(
            PersistenceResolver(new CapturingOutboxWriter("identity")),
            HandlerRegistrations(),
            registry,
            new ModuleUnitOfWorkContext(),
            MessagingOptions());

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => sender.Send(
                new RebuildOperationProjectionCommand(Guid.NewGuid()),
                targetModule: "products"));

        exception.Message.ShouldContain("inside a module unit of work");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenMaxAttemptsIsOverridden_UsesSubmissionValue()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(outboxWriter),
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());

        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            sender.Send(
                new RebuildOperationProjectionCommand(Guid.NewGuid()),
                targetModule: "products",
                maxAttempts: 2,
                partitionKey: "operation-1");
        }

        OutboxMessage message = outboxWriter.Messages.Single();
        message.MaxAttempts.ShouldBe(2);
        message.PartitionKey.ShouldBe("operation-1");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenTargetModuleHasNoCommandHandler_Throws()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(outboxWriter),
            [],
            registry,
            unitOfWorkContext,
            MessagingOptions());

        using IDisposable moduleScope = unitOfWorkContext.StartModuleScope("identity");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => sender.Send(
                new RebuildOperationProjectionCommand(Guid.NewGuid()),
                targetModule: "products"));
        exception.Message.ShouldContain("products");
        exception.Message.ShouldContain(typeof(RebuildOperationProjectionCommand).FullName!);
        outboxWriter.Messages.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Send_WhenTargetModuleHasMultipleCommandHandlers_Throws()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(outboxWriter),
            [
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(RebuildOperationProjectionCommand),
                    typeof(FirstRebuildOperationProjectionCommandHandler),
                    "products.rebuild-operation-projection.v1"),
                new ModuleMessageHandlerRegistration(
                    "products",
                    typeof(RebuildOperationProjectionCommand),
                    typeof(SecondRebuildOperationProjectionCommandHandler),
                    "products.rebuild-operation-projection.v1")
            ],
            registry,
            unitOfWorkContext,
            MessagingOptions());

        using IDisposable moduleScope = unitOfWorkContext.StartModuleScope("identity");

        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            () => sender.Send(
                new RebuildOperationProjectionCommand(Guid.NewGuid()),
                targetModule: "products"));
        exception.Message.ShouldContain("Multiple");
        exception.Message.ShouldContain("products");
        outboxWriter.Messages.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleOwnedOrchestration_WhenResultIsNeeded_SendsWorkWithDurableOperationIdAndLeavesResultForQueries()
    {
        Guid durableOperationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var operationReader = new FakeDurableOperationReader(
            new DurableOperationSnapshot(
                durableOperationId,
                DurableOperationState.Pending,
                ResultJson: null,
                FailureReason: null));
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "products.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            PersistenceResolver(outboxWriter),
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());
        var orchestration = new ExampleModuleOwnedOrchestration(operationReader, sender);

        CommandSubmission submission;
        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            submission = await orchestration.StartAsync(durableOperationId, CancellationToken.None);
        }

        operationReader.RequestedDurableOperationIds.ShouldBe([durableOperationId]);
        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
        submission.DurableOperationId.ShouldBe(durableOperationId);
        OutboxMessage message = outboxWriter.Messages.Single();
        message.DurableOperationId.ShouldBe(durableOperationId);
        message.TargetModule.ShouldBe("products");
    }

    private sealed record RebuildOperationProjectionCommand(Guid DurableOperationId) : IDurableCommand;

    private sealed class FirstRebuildOperationProjectionCommandHandler;

    private sealed class SecondRebuildOperationProjectionCommandHandler;

    private static IOptions<DurableMessagingOptions> MessagingOptions()
    {
        return Options.Create(new DurableMessagingOptions
        {
            MaxAttempts = 7
        });
    }

    private static ModuleMessageHandlerRegistration[] HandlerRegistrations(string moduleName = "products")
    {
        return
        [
            new ModuleMessageHandlerRegistration(
                moduleName,
                typeof(RebuildOperationProjectionCommand),
                typeof(object),
                "products.rebuild-operation-projection.v1")
        ];
    }

    private static IModulePersistenceResolver PersistenceResolver(params IOutboxWriter[] outboxWriters)
    {
        return new TestModulePersistenceResolver(outboxWriters);
    }

    private sealed class ExampleModuleOwnedOrchestration(
        IDurableOperationReader operationReader,
        IDurableCommandSender durableCommandSender)
    {
        public async Task<CommandSubmission> StartAsync(
            Guid durableOperationId,
            CancellationToken cancellationToken)
        {
            DurableOperationSnapshot? operation = await operationReader.GetOperationAsync(
                durableOperationId,
                cancellationToken);

            if (operation is null)
            {
                throw new InvalidOperationException("Durable operation state is required before durable work is sent.");
            }

            return durableCommandSender.Send(
                new RebuildOperationProjectionCommand(operation.DurableOperationId),
                targetModule: "products",
                durableOperationId: operation.DurableOperationId);
        }
    }

    private sealed class FakeDurableOperationReader(DurableOperationSnapshot? operation) : IDurableOperationReader
    {
        public List<Guid> RequestedDurableOperationIds { get; } = [];

        public Task<DurableOperationSnapshot?> GetOperationAsync(
            Guid durableOperationId,
            CancellationToken cancellationToken)
        {
            RequestedDurableOperationIds.Add(durableOperationId);
            return Task.FromResult(operation);
        }
    }

    private sealed class CapturingOutboxWriter(string moduleName) : IOutboxWriter
    {
        public string ModuleName { get; } = moduleName;

        public List<OutboxMessage> Messages { get; } = [];

        public void Write(OutboxMessage outboxMessage) => Messages.Add(outboxMessage);
    }

    private sealed class TestModulePersistenceResolver(IReadOnlyCollection<IOutboxWriter> outboxWriters)
        : IModulePersistenceResolver
    {
        public IModuleDbContext ResolveDbContext(string moduleName)
        {
            throw new NotSupportedException();
        }

        public IModuleUnitOfWork ResolveUnitOfWork(string moduleName)
        {
            throw new NotSupportedException();
        }

        public IOutboxWriter ResolveOutboxWriter(string moduleName)
        {
            IOutboxWriter[] matchingWriters = outboxWriters
                .Where(writer => string.Equals(writer.ModuleName, moduleName, StringComparison.Ordinal))
                .ToArray();

            return matchingWriters.Length switch
            {
                1 => matchingWriters[0],
                0 => throw new InvalidOperationException(
                    $"No outbox writer is registered for source module '{moduleName}'."),
                _ => throw new InvalidOperationException(
                    $"Multiple outbox writers are registered for source module '{moduleName}'."),
            };
        }
    }
}
