using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Operations.Operations.Events;

[DomainEventType("operations.operation", "operations.operation-failed", 1)]
public sealed record OperationFailedDomainEvent(
    Guid OperationId,
    string OperationType,
    string FailureReason) : DomainEvent;
