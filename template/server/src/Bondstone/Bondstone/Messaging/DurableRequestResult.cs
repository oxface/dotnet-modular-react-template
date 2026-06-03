namespace Bondstone.Messaging;

public sealed record DurableRequestResult<TResult>(
    Guid SubmissionId,
    Guid DurableOperationId,
    DurableRequestStatus Status,
    TResult? Result,
    string? FailureReason);
