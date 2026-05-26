namespace ModularTemplate.Operations.Contracts.Operations;

public sealed record OperationDetails(
    Guid OperationId,
    string OperationType,
    OperationStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? FailedAtUtc,
    string? FailureReason,
    string? ResultJson,
    string? MetadataJson);
