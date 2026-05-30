using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.Infrastructure.Transport;
using ModularTemplate.Operations.Contracts.Operations;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Communication;

public sealed class CommunicationPatternExamplesTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task InProcessQueryContractExample_WhenModuleNeedsRead_UsesTargetModuleContract()
    {
        Guid operationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var operationsQueries = new FakeOperationsQueries(
            CreateOperation(operationId, OperationStatus.Running));
        var consumer = new ExampleReadConsumer(operationsQueries);

        OperationStatus? status = await consumer.GetStatusAsync(operationId, CancellationToken.None);

        status.ShouldBe(OperationStatus.Running);
        operationsQueries.RequestedOperationIds.ShouldBe([operationId]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void DurableCommandExample_WhenModuleRequestsAsyncWork_SendsCommandAndGetsAcceptanceOnly()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "ModularTemplate.operations.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            [outboxWriter],
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());
        Guid operationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        CommandSubmission submission;
        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            submission = sender.Send(
                new RebuildOperationProjectionCommand(operationId),
                new DurableCommandSubmissionOptions(
                    SourceModule: "identity",
                    TargetModule: "operations",
                    OperationId: operationId));
        }

        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
        submission.OperationId.ShouldBe(operationId);
        OutboxMessage message = outboxWriter.Messages.Single();
        message.MessageKind.ShouldBe(MessageKind.Command);
        message.SourceModule.ShouldBe("identity");
        message.TargetModule.ShouldBe("operations");
        message.OperationId.ShouldBe(operationId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleOwnedOrchestrationExample_WhenResultIsNeeded_SendsWorkAndLeavesResultForQueries()
    {
        Guid operationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var operationsQueries = new FakeOperationsQueries(
            CreateOperation(operationId, OperationStatus.Pending));
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "ModularTemplate.operations.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            [outboxWriter],
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());
        var orchestration = new ExampleModuleOwnedOrchestration(operationsQueries, sender);

        CommandSubmission submission;
        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            submission = await orchestration.StartAsync(operationId, CancellationToken.None);
        }

        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
        submission.OperationId.ShouldBe(operationId);
        operationsQueries.RequestedOperationIds.ShouldBe([operationId]);
        outboxWriter.Messages.Single().OperationId.ShouldBe(operationId);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ChoreographyExample_WhenModuleObservesFact_HandlesIntegrationEventWithoutCoordinator()
    {
        var projector = new ExampleProjectionHandler();
        var integrationEvent = new OperationProjectionRebuilt(
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            "ready");

        await projector.HandleAsync(integrationEvent, CancellationToken.None);

        projector.ObservedOperations.ShouldBe([integrationEvent.OperationId]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HostOrchestrationExample_WhenUserWorkflowSpansModules_ComposesContractsAndDelegatesDurableSend()
    {
        Guid operationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var operationsQueries = new FakeOperationsQueries(
            CreateOperation(operationId, OperationStatus.Pending));
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "ModularTemplate.operations.rebuild-operation-projection.v1");
        var unitOfWorkContext = new ModuleUnitOfWorkContext();
        var sender = new DurableCommandSender(
            [outboxWriter],
            HandlerRegistrations(),
            registry,
            unitOfWorkContext,
            MessagingOptions());
        var hostWorkflow = new ExampleHostWorkflow(operationsQueries, sender);

        HostWorkflowResponse response;
        using (unitOfWorkContext.StartModuleScope("identity"))
        {
            response = await hostWorkflow.StartAsync(operationId, CancellationToken.None);
        }

        response.OperationId.ShouldBe(operationId);
        response.SubmissionStatus.ShouldBe(CommandSubmissionStatus.Accepted);
        operationsQueries.RequestedOperationIds.ShouldBe([operationId]);
        outboxWriter.Messages.Single().SourceModule.ShouldBe("identity");
    }

    private static OperationDetails CreateOperation(Guid operationId, OperationStatus status)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        return new OperationDetails(
            operationId,
            "template.communication-example",
            status,
            now,
            now,
            CompletedAtUtc: null,
            FailedAtUtc: null,
            FailureReason: null,
            ResultJson: null,
            MetadataJson: null);
    }

    private static IOptions<DurableMessagingOptions> MessagingOptions()
    {
        return Options.Create(new DurableMessagingOptions
        {
            Modules = ["identity", "operations"]
        });
    }

    private static ModuleMessageHandlerRegistration[] HandlerRegistrations()
    {
        return
        [
            new ModuleMessageHandlerRegistration(
                "operations",
                typeof(RebuildOperationProjectionCommand),
                typeof(object),
                "operations.rebuild-operation-projection.v1")
        ];
    }

    private sealed class ExampleReadConsumer(IOperationsQueries operationsQueries)
    {
        public async Task<OperationStatus?> GetStatusAsync(
            Guid operationId,
            CancellationToken cancellationToken)
        {
            OperationDetails? operation = await operationsQueries.GetOperationAsync(
                operationId,
                cancellationToken);

            return operation?.Status;
        }
    }

    private sealed class ExampleModuleOwnedOrchestration(
        IOperationsQueries operationsQueries,
        IDurableCommandSender durableCommandSender)
    {
        public async Task<CommandSubmission> StartAsync(
            Guid operationId,
            CancellationToken cancellationToken)
        {
            OperationDetails operation = await operationsQueries.GetOperationAsync(operationId, cancellationToken)
                ?? throw new InvalidOperationException("Operation state is required before durable work is sent.");

            return durableCommandSender.Send(
                new RebuildOperationProjectionCommand(operation.OperationId),
                new DurableCommandSubmissionOptions(
                    SourceModule: "identity",
                    TargetModule: "operations",
                    OperationId: operation.OperationId));
        }
    }

    private sealed class ExampleHostWorkflow(
        IOperationsQueries operationsQueries,
        IDurableCommandSender durableCommandSender)
    {
        public async Task<HostWorkflowResponse> StartAsync(
            Guid operationId,
            CancellationToken cancellationToken)
        {
            OperationDetails operation = await operationsQueries.GetOperationAsync(operationId, cancellationToken)
                ?? throw new InvalidOperationException("The requested operation does not exist.");

            CommandSubmission submission = durableCommandSender.Send(
                new RebuildOperationProjectionCommand(operation.OperationId),
                new DurableCommandSubmissionOptions(
                    SourceModule: "identity",
                    TargetModule: "operations",
                    OperationId: operation.OperationId));

            return new HostWorkflowResponse(operation.OperationId, submission.Status);
        }
    }

    private sealed class ExampleProjectionHandler
    {
        public List<Guid> ObservedOperations { get; } = [];

        public Task HandleAsync(
            OperationProjectionRebuilt integrationEvent,
            CancellationToken cancellationToken)
        {
            ObservedOperations.Add(integrationEvent.OperationId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOperationsQueries(OperationDetails? operation) : IOperationsQueries
    {
        public List<Guid> RequestedOperationIds { get; } = [];

        public Task<OperationDetails?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken)
        {
            RequestedOperationIds.Add(operationId);
            return Task.FromResult(operation);
        }
    }

    private sealed class CapturingOutboxWriter(string moduleName) : IOutboxWriter
    {
        public string ModuleName { get; } = moduleName;

        public List<OutboxMessage> Messages { get; } = [];

        public void Write(OutboxMessage outboxMessage) => Messages.Add(outboxMessage);
    }

    private sealed record RebuildOperationProjectionCommand(Guid OperationId) : IDurableCommand;

    private sealed record OperationProjectionRebuilt(Guid OperationId, string ProjectionStatus) : IIntegrationEvent;

    private sealed record HostWorkflowResponse(Guid OperationId, CommandSubmissionStatus SubmissionStatus);
}
