namespace ModularTemplate.SharedKernel.Messaging;

/// <summary>
/// Acknowledges durable command submission. The submission result describes
/// whether work was accepted for asynchronous processing and may include an
/// operation id for later status/result queries.
/// </summary>
public sealed record CommandSubmission(
    Guid SubmissionId,
    Guid? OperationId,
    CommandSubmissionStatus Status);
