using ModularTemplate.Operations.Contracts.Operations;
using ModularTemplate.Operations.Operations;
using ModularTemplate.Operations.Operations.Events;
using Shouldly;

namespace ModularTemplate.Operations.Tests.Operations;

public sealed class OperationTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_WhenOperationIsCreated_SetsPendingStateAndRecordsDomainEvent()
    {
        Operation operation = Operation.Create("notifications.send-email", "{\"priority\":\"high\"}");

        operation.OperationType.ShouldBe("notifications.send-email");
        operation.Status.ShouldBe(OperationStatus.Pending);
        operation.MetadataJson.ShouldBe("{\"priority\":\"high\"}");
        operation.DomainEvents.Single().ShouldBeOfType<OperationCreatedDomainEvent>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkCompleted_WhenOperationIsRunning_CompletesAndRaisesEvent()
    {
        Operation operation = Operation.Create("notifications.send-email");
        operation.ClearDomainEvents();
        operation.MarkRunning();

        operation.MarkCompleted("{\"message\":\"sent\"}");

        operation.Status.ShouldBe(OperationStatus.Completed);
        operation.CompletedAtUtc.ShouldNotBeNull();
        operation.ResultJson.ShouldBe("{\"message\":\"sent\"}");
        operation.DomainEvents.Single().ShouldBeOfType<OperationCompletedDomainEvent>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkFailed_WhenOperationIsPending_FailsAndRaisesEvent()
    {
        Operation operation = Operation.Create("notifications.send-email");
        operation.ClearDomainEvents();

        operation.MarkFailed("provider timeout");

        operation.Status.ShouldBe(OperationStatus.Failed);
        operation.FailedAtUtc.ShouldNotBeNull();
        operation.FailureReason.ShouldBe("provider timeout");
        operation.DomainEvents.Single().ShouldBeOfType<OperationFailedDomainEvent>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MarkRunning_WhenOperationIsCompleted_ThrowsInvalidOperationException()
    {
        Operation operation = Operation.Create("notifications.send-email");
        operation.MarkCompleted();

        Should.Throw<InvalidOperationException>(() => operation.MarkRunning());
    }
}
