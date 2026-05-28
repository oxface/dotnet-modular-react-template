using System.Text.Json;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Operations.Contracts.Operations;
using ModularTemplate.SharedKernel.Messaging;
using Shouldly;

namespace ModularTemplate.Identity.Infrastructure.Tests.Persistence;

public sealed class DurableCommandSubmitterTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Submit_WhenCommandIsAccepted_WritesOutboxMessageAndReturnsSubmission()
    {
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "ModularTemplate.operations.rebuild-operation-projection.v1");
        var submitter = new DurableCommandSubmitter([outboxWriter], registry);
        Guid operationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid causationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var command = new RebuildOperationProjectionCommand(operationId);

        CommandSubmission submission = submitter.Submit(
            command,
            new DurableCommandSubmissionOptions(
                SourceModule: "identity",
                TargetModule: "operations",
                OperationId: operationId,
                CausationId: causationId,
                Metadata: "{\"source\":\"test\"}"));

        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
        submission.OperationId.ShouldBe(operationId);
        OutboxMessage message = outboxWriter.Messages.Single();
        message.MessageId.ShouldBe(submission.SubmissionId);
        message.MessageKind.ShouldBe(MessageKind.Command);
        message.MessageType.ShouldBe("ModularTemplate.operations.rebuild-operation-projection.v1");
        message.SourceModule.ShouldBe("identity");
        message.TargetModule.ShouldBe("operations");
        message.CorrelationId.ShouldBe(submission.SubmissionId);
        message.CausationId.ShouldBe(causationId);
        message.OperationId.ShouldBe(operationId);
        message.Metadata.ShouldBe("{\"source\":\"test\"}");
        JsonSerializer.Deserialize<RebuildOperationProjectionCommand>(message.Payload)
            .ShouldBe(command);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Submit_WhenSourceModuleHasNoOutboxWriter_Throws()
    {
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "ModularTemplate.operations.rebuild-operation-projection.v1");
        var submitter = new DurableCommandSubmitter([new CapturingOutboxWriter("identity")], registry);

        Should.Throw<InvalidOperationException>(
            () => submitter.Submit(
                new RebuildOperationProjectionCommand(Guid.NewGuid()),
                new DurableCommandSubmissionOptions(
                    SourceModule: "operations",
                    TargetModule: "identity")));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleOwnedOrchestration_WhenResultIsNeeded_SubmitsWorkWithOperationIdAndLeavesResultForQueries()
    {
        Guid operationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var operationsQueries = new FakeOperationsQueries(
            new OperationDetails(
                operationId,
                "template.communication-example",
                OperationStatus.Pending,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                CompletedAtUtc: null,
                FailedAtUtc: null,
                FailureReason: null,
                ResultJson: null,
                MetadataJson: null));
        var outboxWriter = new CapturingOutboxWriter("identity");
        var registry = new MessageTypeRegistry();
        registry.Register<RebuildOperationProjectionCommand>(
            "ModularTemplate.operations.rebuild-operation-projection.v1");
        var submitter = new DurableCommandSubmitter([outboxWriter], registry);
        var orchestration = new ExampleModuleOwnedOrchestration(operationsQueries, submitter);

        CommandSubmission submission = await orchestration.StartAsync(operationId, CancellationToken.None);

        operationsQueries.RequestedOperationIds.ShouldBe([operationId]);
        submission.Status.ShouldBe(CommandSubmissionStatus.Accepted);
        submission.OperationId.ShouldBe(operationId);
        OutboxMessage message = outboxWriter.Messages.Single();
        message.OperationId.ShouldBe(operationId);
        message.TargetModule.ShouldBe("operations");
    }

    private sealed record RebuildOperationProjectionCommand(Guid OperationId) : IDurableCommand;

    private sealed class ExampleModuleOwnedOrchestration(
        IOperationsQueries operationsQueries,
        IDurableCommandSubmitter durableCommandSubmitter)
    {
        public async Task<CommandSubmission> StartAsync(
            Guid operationId,
            CancellationToken cancellationToken)
        {
            OperationDetails? operation = await operationsQueries.GetOperationAsync(operationId, cancellationToken);

            if (operation is null)
            {
                throw new InvalidOperationException("Operation state is required before durable work is submitted.");
            }

            return durableCommandSubmitter.Submit(
                new RebuildOperationProjectionCommand(operation.OperationId),
                new DurableCommandSubmissionOptions(
                    SourceModule: "identity",
                    TargetModule: "operations",
                    OperationId: operation.OperationId));
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
}
