namespace ModularTemplate.SharedKernel.Messaging;

public sealed record CommandSubmission(
    Guid SubmissionId,
    Guid? OperationId,
    CommandSubmissionStatus Status);
