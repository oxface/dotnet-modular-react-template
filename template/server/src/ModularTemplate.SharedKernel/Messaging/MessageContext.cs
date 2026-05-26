namespace ModularTemplate.SharedKernel.Messaging;

public sealed record MessageContext(
    Guid MessageId,
    string SourceModule,
    string TargetModule,
    Guid CorrelationId,
    Guid? CausationId,
    Guid? OperationId,
    string? IdempotencyKey,
    DateTimeOffset CreatedAtUtc);
