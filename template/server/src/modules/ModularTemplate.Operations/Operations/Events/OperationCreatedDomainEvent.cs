using Bondstone.Domain;
using ModularTemplate.SharedKernel.Domain;

namespace ModularTemplate.Operations.Operations.Events;

[DomainEventType("operations.operation", "operations.operation-created", 1)]
public sealed record OperationCreatedDomainEvent(
    Guid OperationId,
    string OperationType) : DomainEvent;
