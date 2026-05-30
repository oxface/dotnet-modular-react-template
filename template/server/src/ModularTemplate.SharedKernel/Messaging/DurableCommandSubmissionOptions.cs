namespace ModularTemplate.SharedKernel.Messaging;

public sealed record DurableCommandSubmissionOptions(
    string SourceModule,
    string TargetModule,
    Guid? OperationId = null,
    Guid? CorrelationId = null,
    Guid? CausationId = null,
    int? MaxAttempts = null);
