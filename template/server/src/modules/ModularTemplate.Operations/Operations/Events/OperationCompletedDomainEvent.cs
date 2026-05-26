using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Operations.Operations.Events;

[DomainEventType("operations.operation", "operations.operation-completed", 1)]
public sealed record OperationCompletedDomainEvent(
    Guid OperationId,
    string OperationType) : DomainEvent;
