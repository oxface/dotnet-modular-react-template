namespace Bondstone.Messaging;

public sealed record DurableOperationSnapshot(
    Guid DurableOperationId,
    DurableOperationState State,
    string? ResultJson,
    string? FailureReason);
