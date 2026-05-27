using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.SharedKernel.Messaging;

namespace ModularTemplate.Infrastructure.Transport;

public sealed record DurableTransportEnvelope(
    Guid MessageId,
    MessageKind MessageKind,
    string MessageType,
    string SourceModule,
    string TargetModule,
    Guid CorrelationId,
    Guid? CausationId,
    Guid? OperationId,
    string Payload,
    string? MetadataJson,
    DateTimeOffset CreatedAtUtc,
    int MaxAttempts);
